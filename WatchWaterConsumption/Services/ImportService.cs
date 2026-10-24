using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IImportService
{
    Task<ImportResultDto> ImportAsync(Stream stream, string fileName, ImportOptions options, string userName, CancellationToken cancellationToken = default);
    Task<LayerDto> RegisterServiceLayerAsync(ServiceLayerCreateRequest request, CancellationToken cancellationToken = default);
    Task<LayerDto> RegisterRasterMetadataAsync(RasterMetadataRequest request, CancellationToken cancellationToken = default);
}

public class ImportService : IImportService
{
    private static readonly string[] AllowedExtensions = { ".geojson", ".json", ".csv", ".kml", ".zip", ".gpkg", ".shp" };
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;

    public ImportService(DatabaseContext db, IGeoJsonService geoJson)
    {
        _db = db;
        _geoJson = geoJson;
    }

    public async Task<ImportResultDto> ImportAsync(Stream stream, string fileName, ImportOptions options, string userName, CancellationToken cancellationToken = default)
    {
        fileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"Unsupported format '{extension}'. Supported: GeoJSON, CSV, KML, zipped Shapefile, GeoPackage. GeoTIFF is metadata-registration only.");
        }
        if (string.IsNullOrWhiteSpace(options.LayerKey) || string.IsNullOrWhiteSpace(options.LayerName))
        {
            throw new ArgumentException("Layer key and layer name are required.");
        }

        var errors = new List<ImportErrorDto>();
        List<GeoJsonFeatureInput> parsed;
        var format = extension.TrimStart('.').ToUpperInvariant();
        try
        {
            parsed = extension switch
            {
                ".geojson" or ".json" => await ParseGeoJsonAsync(stream, options.DeclaredSrid, errors, cancellationToken),
                ".csv" => await ParseCsvAsync(stream, options.DeclaredSrid, errors, cancellationToken),
                ".kml" => await ParseKmlAsync(stream, options.DeclaredSrid, errors, cancellationToken),
                ".zip" => await ParseShapefileZipAsync(stream, options.DeclaredSrid, errors, cancellationToken),
                ".gpkg" => await ParseGeoPackageAsync(stream, errors, cancellationToken),
                ".shp" => throw new ArgumentException("A .shp file requires its .shx, .dbf and .prj companions. Upload them together as a .zip."),
                _ => throw new ArgumentException("Unsupported format.")
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException($"Import parsing failed: {ex.Message}", ex);
        }

        if (parsed.Count == 0 && errors.Count == 0)
        {
            errors.Add(new ImportErrorDto(0, "No features were found in the file."));
        }
        if (parsed.Count > 50000)
        {
            throw new ArgumentException("Imports are limited to 50,000 features per file.");
        }

        var layer = await _db.SpatialLayers.Include(l => l.DataSource)
            .FirstOrDefaultAsync(l => l.Key == options.LayerKey, cancellationToken);
        var existingExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (layer is not null)
        {
            var existing = await _db.SpatialFeatures.AsNoTracking()
                .Where(f => f.LayerId == layer.Id)
                .Select(f => new { f.ExternalId, f.GeometryHash })
                .ToListAsync(cancellationToken);
            existingExternalIds.UnionWith(existing.Where(e => e.ExternalId != null).Select(e => e.ExternalId!));
            existingHashes.UnionWith(existing.Where(e => e.GeometryHash != null).Select(e => e.GeometryHash!));
        }

        var accepted = new List<(GeoJsonFeatureInput Feature, string Hash)>();
        var acceptedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var row = 0;
        foreach (var feature in parsed)
        {
            row++;
            var validation = _geoJson.Validate(feature.Geometry);
            if (validation.Count > 0)
            {
                errors.Add(new ImportErrorDto(row, string.Join(" ", validation)));
                continue;
            }
            var hash = _geoJson.ComputeGeometryHash(feature.Geometry);
            var externalId = feature.Id ?? GetProperty(feature, "osm_id") ?? GetProperty(feature, "id");
            if ((externalId is not null && existingExternalIds.Contains(externalId)) ||
                existingHashes.Contains(hash) ||
                (externalId is not null && acceptedIds.Contains(externalId)))
            {
                duplicates++;
                continue;
            }
            if (externalId is not null)
            {
                existingExternalIds.Add(externalId);
                acceptedIds.Add(externalId);
            }
            existingHashes.Add(hash);
            accepted.Add((feature, hash));
        }

        var dataSource = await ResolveDataSourceAsync(options, cancellationToken);
        if (layer is null)
        {
            layer = new SpatialLayer
            {
                Key = options.LayerKey.Trim(), Name = options.LayerName.Trim(), Category = options.Category,
                Description = options.Description, GeometryType = "Geometry",
                IsPublic = options.IsPublic, DataClassification = DataClassification.Imported,
                DataSource = dataSource
            };
            _db.SpatialLayers.Add(layer);
        }
        else
        {
            layer.Name = options.LayerName.Trim();
            layer.Category = options.Category;
            layer.Description = options.Description ?? layer.Description;
            layer.IsPublic = options.IsPublic;
            layer.DataClassification = DataClassification.Imported;
            layer.DataSource = dataSource ?? layer.DataSource;
        }

        var batch = new ImportBatch
        {
            FileName = fileName, TargetLayerKey = options.LayerKey, Format = format,
            RequestedBy = userName, Status = options.Commit ? ImportStatus.Imported : ImportStatus.Preview,
            RowsTotal = parsed.Count + errors.Count(e => e.Row == 0), RowsAccepted = accepted.Count,
            RowsRejected = errors.Count - errors.Count(e => e.Row == 0), ErrorsJson = errors.Count == 0 ? null : JsonSerializer.Serialize(errors),
            ParametersJson = JsonSerializer.Serialize(new
            {
                options.DeclaredSrid, options.SourceName, options.SourcePublisher, options.Licence,
                options.SourceUrl, options.AcquiredAt, duplicates, options.Commit
            }),
            DataSource = dataSource
        };
        _db.ImportBatches.Add(batch);

        if (!options.Commit)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return BuildResult(batch, format, options, accepted.Count, duplicates, errors, accepted, 0, false,
                "Preview only. No features were written. Re-submit with commit=true to persist.");
        }

        if (accepted.Count == 0)
        {
            batch.Status = ImportStatus.Failed;
            await _db.SaveChangesAsync(cancellationToken);
            return BuildResult(batch, format, options, 0, duplicates, errors, accepted, 0, false,
                "No valid features were available to commit.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var (feature, hash) in accepted)
            {
                _db.SpatialFeatures.Add(new SpatialFeature
                {
                    Layer = layer,
                    ExternalId = feature.Id ?? GetProperty(feature, "osm_id") ?? GetProperty(feature, "id"),
                    Name = GetProperty(feature, "name") ?? GetProperty(feature, "Name"),
                    Geometry = feature.Geometry,
                    AttributesJson = JsonSerializer.Serialize(feature.Properties),
                    SourceUrl = options.SourceUrl ?? GetProperty(feature, "osm_url"),
                    GeometryHash = hash,
                    DataClassification = DataClassification.Imported,
                    ImportedAt = DateTimeOffset.UtcNow
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
            var featureCount = await _db.SpatialFeatures.CountAsync(f => f.LayerId == layer.Id, cancellationToken);
            await _db.SpatialLayers.Where(l => l.Id == layer.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.FeatureCount, featureCount), cancellationToken);
            layer.FeatureCount = featureCount;
            batch.Status = errors.Count == 0 ? ImportStatus.Imported : ImportStatus.PartiallyImported;
            batch.RowsAccepted = accepted.Count;
            batch.RowsRejected = parsed.Count - accepted.Count + errors.Count(e => e.Row == 0);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return BuildResult(batch, format, options, accepted.Count, duplicates, errors, accepted, layer.FeatureCount, true,
                $"Committed {accepted.Count} feature(s) to layer '{layer.Key}'.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            batch.Status = ImportStatus.Failed;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LayerDto> RegisterServiceLayerAsync(ServiceLayerCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ServiceType is not ("WMS" or "WFS"))
        {
            throw new ArgumentException("Service type must be WMS or WFS.");
        }
        var layer = await _db.SpatialLayers.FirstOrDefaultAsync(l => l.Key == request.LayerKey, cancellationToken);
        if (layer is null)
        {
            layer = new SpatialLayer { Key = request.LayerKey.Trim() };
            _db.SpatialLayers.Add(layer);
        }
        layer.Name = request.LayerName.Trim();
        layer.Category = request.Category;
        layer.ServiceType = request.ServiceType;
        layer.ServiceUrl = request.ServiceUrl;
        layer.ServiceLayerName = request.ServiceLayerName;
        layer.IsPublic = request.IsPublic;
        layer.DataClassification = DataClassification.Imported;
        layer.GeometryType = "Service";
        await _db.SaveChangesAsync(cancellationToken);
        return new LayerDto(layer.Key, layer.Name, layer.Category, layer.GeometryType, layer.FeatureCount,
            layer.IsPublic, false, layer.StyleJson, layer.DataClassification, request.ServiceType, request.Licence,
            layer.ServiceType, layer.ServiceUrl, layer.ServiceLayerName);
    }

    public async Task<LayerDto> RegisterRasterMetadataAsync(RasterMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MinLon is not null && (request.MinLon < -180 || request.MaxLon > 180 || request.MinLat < -90 || request.MaxLat > 90))
        {
            throw new ArgumentException("Raster bounds are outside valid WGS84 coordinates.");
        }
        var layer = await _db.SpatialLayers.FirstOrDefaultAsync(l => l.Key == request.LayerKey, cancellationToken);
        if (layer is null)
        {
            layer = new SpatialLayer { Key = request.LayerKey.Trim() };
            _db.SpatialLayers.Add(layer);
        }
        layer.Name = request.LayerName.Trim();
        layer.Category = "Raster metadata";
        layer.Description = request.Description;
        layer.ServiceType = "GeoTIFF metadata";
        layer.GeometryType = "Raster";
        layer.StyleJson = JsonSerializer.Serialize(new
        {
            request.Srid, request.MinLon, request.MinLat, request.MaxLon, request.MaxLat,
            request.ResolutionMeters, request.AcquisitionDate, request.SourceName, request.Licence, request.MetadataJson
        });
        layer.IsPublic = true;
        layer.DataClassification = DataClassification.Imported;
        await _db.SaveChangesAsync(cancellationToken);
        return new LayerDto(layer.Key, layer.Name, layer.Category, layer.GeometryType, 0, true, false,
            layer.StyleJson, layer.DataClassification, request.SourceName, request.Licence);
    }

    private async Task<DataSource?> ResolveDataSourceAsync(ImportOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.SourceName))
        {
            return null;
        }
        var source = await _db.DataSources.FirstOrDefaultAsync(d => d.Name == options.SourceName, cancellationToken);
        if (source is null)
        {
            source = new DataSource
            {
                Name = options.SourceName.Trim(), Publisher = (options.SourcePublisher ?? options.SourceName).Trim(),
                Licence = options.Licence, LicenceUrl = options.LicenceUrl, SourceUrl = options.SourceUrl,
                AcquiredAt = options.AcquiredAt ?? DateTimeOffset.UtcNow,
                QualityNotes = "Imported through the WaterWatch ingestion pipeline; validation report stored with the import batch.",
                IsOfficial = false, DataClassification = DataClassification.Imported
            };
            _db.DataSources.Add(source);
        }
        else
        {
            source.Licence ??= options.Licence;
            source.LicenceUrl ??= options.LicenceUrl;
            source.SourceUrl ??= options.SourceUrl;
            source.AcquiredAt ??= options.AcquiredAt;
        }
        return source;
    }

    private async Task<List<GeoJsonFeatureInput>> ParseGeoJsonAsync(Stream stream, int declaredSrid, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, leaveOpen: true);
        var json = await reader.ReadToEndAsync(cancellationToken);
        var result = _geoJson.ParseFeatureCollection(json, declaredSrid);
        errors.AddRange(result.Errors.Select(e => new ImportErrorDto(0, e)));
        return result.Features.ToList();
    }

    private async Task<List<GeoJsonFeatureInput>> ParseCsvAsync(Stream stream, int declaredSrid, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim, BadDataFound = null, MissingFieldFound = null, PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
        });
        if (!await csv.ReadAsync())
        {
            errors.Add(new ImportErrorDto(0, "The CSV file is empty."));
            return new List<GeoJsonFeatureInput>();
        }
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var lonIndex = FindHeader(headers, "lon", "longitude", "x");
        var latIndex = FindHeader(headers, "lat", "latitude", "y");
        var wktIndex = FindHeader(headers, "wkt", "geometry");
        if (lonIndex < 0 && wktIndex < 0)
        {
            errors.Add(new ImportErrorDto(0, "CSV must contain lon/lat columns or a WKT geometry column."));
            return new List<GeoJsonFeatureInput>();
        }
        var features = new List<GeoJsonFeatureInput>();
        var wktReader = new WKTReader(SpatialMath.Wgs84Factory);
        var row = 1;
        while (await csv.ReadAsync())
        {
            row++;
            try
            {
                Geometry? geometry = null;
                if (wktIndex >= 0 && !string.IsNullOrWhiteSpace(csv.GetField(wktIndex)))
                {
                    geometry = wktReader.Read(csv.GetField(wktIndex));
                }
                else if (lonIndex >= 0 && latIndex >= 0 &&
                         double.TryParse(csv.GetField(lonIndex), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
                         double.TryParse(csv.GetField(latIndex), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                {
                    geometry = SpatialMath.Point(lon, lat);
                }
                if (geometry is null)
                {
                    errors.Add(new ImportErrorDto(row, "No valid coordinate geometry in this row."));
                    continue;
                }
                if (declaredSrid != SpatialMath.Wgs84)
                {
                    geometry = SpatialMath.Transform(geometry, declaredSrid, SpatialMath.Wgs84);
                }
                geometry.SRID = SpatialMath.Wgs84;
                var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Length; i++)
                {
                    if (i == wktIndex)
                    {
                        continue;
                    }
                    properties[headers[i]] = csv.GetField(i);
                }
                features.Add(new GeoJsonFeatureInput(GetCsvId(properties), geometry, properties, null));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportErrorDto(row, ex.Message));
            }
        }
        return features;
    }

    private async Task<List<GeoJsonFeatureInput>> ParseKmlAsync(Stream stream, int declaredSrid, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var features = new List<GeoJsonFeatureInput>();
        var geometryFactory = SpatialMath.Wgs84Factory;
        var row = 0;
        foreach (var placemark in document.Descendants().Where(e => e.Name.LocalName == "Placemark"))
        {
            row++;
            try
            {
                var name = placemark.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value;
                var description = placemark.Elements().FirstOrDefault(e => e.Name.LocalName == "description")?.Value;
                var geometries = new List<Geometry>();
                foreach (var element in placemark.Descendants())
                {
                    if (element.Name.LocalName == "Point")
                    {
                        var coordinates = element.Elements().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
                        var point = ParseCoordinates(coordinates).FirstOrDefault();
                        if (point is not null)
                        {
                            geometries.Add(geometryFactory.CreatePoint(point));
                        }
                    }
                    else if (element.Name.LocalName == "LineString")
                    {
                        var coordinates = element.Elements().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
                        var coords = ParseCoordinates(coordinates);
                        if (coords.Count >= 2)
                        {
                            geometries.Add(geometryFactory.CreateLineString(coords.ToArray()));
                        }
                    }
                    else if (element.Name.LocalName == "Polygon")
                    {
                        var outer = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "outerBoundaryIs")
                            ?.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
                        var outerCoords = ParseCoordinates(outer);
                        if (outerCoords.Count >= 4)
                        {
                            if (!outerCoords[0].Equals2D(outerCoords[^1]))
                            {
                                outerCoords.Add(outerCoords[0]);
                            }
                            var shell = geometryFactory.CreateLinearRing(outerCoords.ToArray());
                            var holes = element.Descendants().Where(e => e.Name.LocalName == "innerBoundaryIs")
                                .Select(e => e.Descendants().FirstOrDefault(x => x.Name.LocalName == "coordinates")?.Value)
                                .Select(ParseCoordinates)
                                .Where(c => c.Count >= 4)
                                .Select(c => geometryFactory.CreateLinearRing(c.ToArray()))
                                .ToArray();
                            geometries.Add(geometryFactory.CreatePolygon(shell, holes));
                        }
                    }
                }
                if (geometries.Count == 0)
                {
                    errors.Add(new ImportErrorDto(row, "KML Placemark has no supported geometry."));
                    continue;
                }
                var geometry = geometries.Count == 1 ? geometries[0] : geometryFactory.BuildGeometry(geometries);
                if (declaredSrid != SpatialMath.Wgs84)
                {
                    geometry = SpatialMath.Transform(geometry, declaredSrid, SpatialMath.Wgs84);
                }
                geometry.SRID = SpatialMath.Wgs84;
                features.Add(new GeoJsonFeatureInput(null, geometry, new Dictionary<string, object?>
                {
                    ["name"] = name, ["description"] = description, ["source"] = "KML"
                }, null));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportErrorDto(row, ex.Message));
            }
        }
        return features;
    }

    private async Task<List<GeoJsonFeatureInput>> ParseShapefileZipAsync(Stream stream, int declaredSrid, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"waterwatch-shp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var zipPath = Path.Combine(tempRoot, "upload.zip");
            await using (var file = File.Create(zipPath))
            {
                await stream.CopyToAsync(file, cancellationToken);
            }
            var extractDir = Path.Combine(tempRoot, "extract");
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            var shapePath = Directory.EnumerateFiles(extractDir, "*.shp", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new ArgumentException("The zip does not contain a .shp file.");
            var features = new List<GeoJsonFeatureInput>();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var reader = new ShapefileDataReader(shapePath, SpatialMath.Wgs84Factory);
            var row = 0;
            while (reader.Read())
            {
                row++;
                try
                {
                    var geometry = reader.Geometry;
                    if (geometry is null || geometry.IsEmpty)
                    {
                        errors.Add(new ImportErrorDto(row, "Empty shapefile geometry."));
                        continue;
                    }
                    if (declaredSrid != SpatialMath.Wgs84)
                    {
                        geometry = SpatialMath.Transform(geometry, declaredSrid, SpatialMath.Wgs84);
                    }
                    geometry.SRID = SpatialMath.Wgs84;
                    var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        properties[reader.GetName(i)] = reader.GetValue(i) is DBNull ? null : reader.GetValue(i);
                    }
                    features.Add(new GeoJsonFeatureInput(null, geometry, properties, null));
                }
                catch (Exception ex)
                {
                    errors.Add(new ImportErrorDto(row, ex.Message));
                }
            }
            return features;
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { /* temp cleanup is best effort */ }
        }
    }

    private async Task<List<GeoJsonFeatureInput>> ParseGeoPackageAsync(Stream stream, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"waterwatch-{Guid.NewGuid():N}.gpkg");
        try
        {
            await using (var file = File.Create(temp))
            {
                await stream.CopyToAsync(file, cancellationToken);
            }
            var connectionString = new SqliteConnectionStringBuilder { DataSource = temp, Mode = SqliteOpenMode.ReadOnly }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT table_name, column_name, srs_id FROM gpkg_geometry_columns";
            var tables = new List<(string Table, string Column, int SrsId)>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    tables.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
                }
            }
            if (tables.Count == 0)
            {
                throw new ArgumentException("The GeoPackage contains no geometry columns.");
            }
            var geometryReader = new GeoPackageGeoReader();
            var features = new List<GeoJsonFeatureInput>();
            var row = 0;
            foreach (var (table, column, srsId) in tables)
            {
                if (!table.All(c => char.IsLetterOrDigit(c) || c == '_'))
                {
                    errors.Add(new ImportErrorDto(0, $"Skipped GeoPackage table with unsafe name '{table}'."));
                    continue;
                }
                await using var tableCommand = connection.CreateCommand();
                tableCommand.CommandText = $"SELECT * FROM \"{table}\"";
                await using var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken);
                while (await tableReader.ReadAsync(cancellationToken))
                {
                    row++;
                    try
                    {
                        var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        byte[]? blob = null;
                        for (var i = 0; i < tableReader.FieldCount; i++)
                        {
                            var name = tableReader.GetName(i);
                            if (name.Equals(column, StringComparison.OrdinalIgnoreCase))
                            {
                                blob = tableReader.GetValue(i) as byte[];
                            }
                            else
                            {
                                properties[name] = tableReader.GetValue(i) is DBNull ? null : tableReader.GetValue(i);
                            }
                        }
                        if (blob is null)
                        {
                            errors.Add(new ImportErrorDto(row, "GeoPackage row has no geometry blob."));
                            continue;
                        }
                        var geometry = geometryReader.Read(blob);
                        if (srsId is not (4326 or 3857 or 32632))
                        {
                            errors.Add(new ImportErrorDto(row, $"GeoPackage SRS {srsId} is not supported; reproject to EPSG:4326 before import."));
                            continue;
                        }
                        if (srsId != SpatialMath.Wgs84)
                        {
                            geometry = SpatialMath.Transform(geometry, srsId, SpatialMath.Wgs84);
                        }
                        geometry.SRID = SpatialMath.Wgs84;
                        features.Add(new GeoJsonFeatureInput(null, geometry, properties, null));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ImportErrorDto(row, ex.Message));
                    }
                }
            }
            return features;
        }
        finally
        {
            try { File.Delete(temp); } catch { /* temp cleanup is best effort */ }
        }
    }

    private static List<Coordinate> ParseCoordinates(string? value)
    {
        var coordinates = new List<Coordinate>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return coordinates;
        }
        foreach (var tuple in value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = tuple.Split(',');
            if (parts.Length >= 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            {
                coordinates.Add(new Coordinate(lon, lat));
            }
        }
        return coordinates;
    }

    private static int FindHeader(string[] headers, params string[] names)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            if (names.Any(n => headers[i].Equals(n, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }
        return -1;
    }

    private static string? GetCsvId(Dictionary<string, object?> properties) =>
        properties.TryGetValue("id", out var id) ? id?.ToString() :
        properties.TryGetValue("external_id", out var externalId) ? externalId?.ToString() : null;

    private static string? GetProperty(GeoJsonFeatureInput feature, string key) =>
        feature.Properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static ImportResultDto BuildResult(ImportBatch batch, string format, ImportOptions options,
        int accepted, int duplicates, List<ImportErrorDto> errors, List<(GeoJsonFeatureInput Feature, string Hash)> features,
        int layerCount, bool committed, string message)
    {
        var sample = features.FirstOrDefault().Feature?.Properties
            ?.Take(12).ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<string, object?>();
        return new ImportResultDto(batch.Id, batch.Status.ToString(), format, options.LayerKey,
            batch.RowsTotal, accepted, batch.RowsRejected, duplicates, errors.Take(200).ToList(),
            sample, committed, message);
    }
}
