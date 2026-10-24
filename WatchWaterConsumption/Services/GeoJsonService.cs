using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WaterWatch.Services;

public sealed record GeoJsonFeatureInput(string? Id, Geometry Geometry, Dictionary<string, object?> Properties, string? GeometryJson);

public sealed record GeoJsonFeatureOutput(string? Id, Geometry Geometry, Dictionary<string, object?> Properties);

public sealed record GeoJsonParseResult(IReadOnlyList<GeoJsonFeatureInput> Features, IReadOnlyList<string> Errors);

public interface IGeoJsonService
{
    GeoJsonParseResult ParseFeatureCollection(string json, int declaredSrid = SpatialMath.Wgs84);
    string SerializeFeatureCollection(IEnumerable<GeoJsonFeatureOutput> features);
    string SerializeGeometry(Geometry? geometry);
    Geometry? ParseGeometry(string json);
    string ComputeGeometryHash(Geometry geometry);
    IReadOnlyList<string> Validate(Geometry geometry);
}

public class GeoJsonService : IGeoJsonService
{
    private readonly GeoJsonReader _reader = new(SpatialMath.Wgs84Factory, new JsonSerializerSettings());
    private readonly GeoJsonWriter _writer = new();

    public GeoJsonParseResult ParseFeatureCollection(string json, int declaredSrid = SpatialMath.Wgs84)
    {
        var features = new List<GeoJsonFeatureInput>();
        var errors = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("features", out var featureArray))
            {
                return new GeoJsonParseResult(features, new[] { "The document has no 'features' array." });
            }

            var index = 0;
            foreach (var feature in featureArray.EnumerateArray())
            {
                index++;
                try
                {
                    if (!feature.TryGetProperty("geometry", out var geometryElement) || geometryElement.ValueKind == JsonValueKind.Null)
                    {
                        errors.Add($"Feature {index}: geometry is missing.");
                        continue;
                    }

                    var geometry = _reader.Read<Geometry>(geometryElement.GetRawText());
                    if (geometry is null || geometry.IsEmpty)
                    {
                        errors.Add($"Feature {index}: geometry is empty.");
                        continue;
                    }

                    if (declaredSrid != SpatialMath.Wgs84)
                    {
                        geometry = SpatialMath.Transform(geometry, declaredSrid, SpatialMath.Wgs84);
                    }
                    geometry.SRID = SpatialMath.Wgs84;

                    var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    if (feature.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in props.EnumerateObject())
                        {
                            properties[property.Name] = ConvertElement(property.Value);
                        }
                    }

                    var id = feature.TryGetProperty("id", out var idElement)
                        ? idElement.ToString()
                        : properties.TryGetValue("osm_id", out var osmId) ? osmId?.ToString() : null;

                    features.Add(new GeoJsonFeatureInput(id, geometry, properties, geometryElement.GetRawText()));
                }
                catch (Exception ex)
                {
                    errors.Add($"Feature {index}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid GeoJSON: {ex.Message}");
        }

        return new GeoJsonParseResult(features, errors);
    }

    public string SerializeFeatureCollection(IEnumerable<GeoJsonFeatureOutput> features)
    {
        var array = new JArray();
        foreach (var feature in features)
        {
            var properties = new JObject();
            foreach (var (key, value) in feature.Properties)
            {
                properties[key] = value is null ? JValue.CreateNull() : JToken.FromObject(value);
            }

            array.Add(new JObject
            {
                ["type"] = "Feature",
                ["id"] = feature.Id,
                ["geometry"] = feature.Geometry is null ? JValue.CreateNull() : JToken.Parse(_writer.Write(feature.Geometry)),
                ["properties"] = properties
            });
        }

        return new JObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = array
        }.ToString(Formatting.None);
    }

    public string SerializeGeometry(Geometry? geometry) =>
        geometry is null ? "null" : _writer.Write(geometry);

    public Geometry? ParseGeometry(string json)
    {
        var geometry = _reader.Read<Geometry>(json);
        if (geometry is null)
        {
            return null;
        }
        geometry.SRID = SpatialMath.Wgs84;
        return geometry;
    }

    public string ComputeGeometryHash(Geometry geometry)
    {
        var normalized = geometry.Normalized();
        var text = normalized.AsText();
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    public IReadOnlyList<string> Validate(Geometry geometry)
    {
        var errors = new List<string>();
        if (geometry.IsEmpty)
        {
            errors.Add("Geometry is empty.");
            return errors;
        }

        if (!geometry.IsValid)
        {
            errors.Add($"Geometry is not valid: {new NetTopologySuite.Operation.Valid.IsValidOp(geometry).ValidationError?.Message ?? "unknown validity error"}");
        }

        var envelope = geometry.EnvelopeInternal;
        if (envelope.IsNull)
        {
            errors.Add("Geometry has no envelope.");
        }
        else if (envelope.MinX < -180 || envelope.MaxX > 180 || envelope.MinY < -90 || envelope.MaxY > 90)
        {
            errors.Add("Geometry coordinates fall outside valid WGS84 longitude/latitude bounds.");
        }
        else if (envelope.MinX < 2 || envelope.MaxX > 15 || envelope.MinY < 3 || envelope.MaxY > 15)
        {
            errors.Add("Geometry is outside the configured Nigeria/Rivers State operating envelope (2–15°E, 3–15°N).");
        }

        return errors;
    }

    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToArray(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertElement(p.Value)),
        _ => element.GetRawText()
    };
}
