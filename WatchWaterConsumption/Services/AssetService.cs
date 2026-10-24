using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Security;

namespace WaterWatch.Services;

public interface IAssetService
{
    Task<PagedResult<AssetSummaryDto>> GetAssetsAsync(AssetListQuery query, CancellationToken cancellationToken = default);
    Task<AssetDetailDto?> GetAssetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AssetDetailDto> CreateAssetAsync(AssetCreateRequest request, string userName, CancellationToken cancellationToken = default);
    Task<AssetDetailDto> UpdateAssetAsync(Guid id, AssetUpdateRequest request, string userName, CancellationToken cancellationToken = default);
    Task<bool> DeleteAssetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AssetStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task<InspectionDto> AddInspectionAsync(Guid assetId, InspectionCreateRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceDto> AddMaintenanceAsync(Guid assetId, MaintenanceCreateRequest request, CancellationToken cancellationToken = default);
    Task<bool> CanViewSensitiveAsync();
}

public class AssetService : IAssetService
{
    private readonly DatabaseContext _db;
    private readonly IGeoJsonService _geoJson;
    private readonly IHttpContextAccessor _http;

    public AssetService(DatabaseContext db, IGeoJsonService geoJson, IHttpContextAccessor http)
    {
        _db = db;
        _geoJson = geoJson;
        _http = http;
    }

    public Task<bool> CanViewSensitiveAsync() => Task.FromResult(CanViewSensitive);

    private bool CanViewSensitive =>
        _http.HttpContext?.User is { Identity.IsAuthenticated: true } user &&
        Policies.Definitions.First(p => p.Name == Policies.CanViewSensitive).Roles.Any(user.IsInRole);

    public async Task<PagedResult<AssetSummaryDto>> GetAssetsAsync(AssetListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var assets = _db.Assets.AsNoTracking()
            .Include(a => a.AdminArea).Include(a => a.OperationalArea).Include(a => a.DataSource)
            .AsQueryable();

        if (!CanViewSensitive)
        {
            assets = assets.Where(a => !a.IsSensitive);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            assets = assets.Where(a => EF.Functions.ILike(a.Name, pattern) || EF.Functions.ILike(a.AssetCode, pattern));
        }
        if (query.Type is not null)
        {
            assets = assets.Where(a => a.AssetType == query.Type);
        }
        if (query.Status is not null)
        {
            assets = assets.Where(a => a.Status == query.Status);
        }
        if (query.AdminAreaId is not null)
        {
            assets = assets.Where(a => a.AdminAreaId == query.AdminAreaId);
        }
        if (query.OperationalAreaId is not null)
        {
            assets = assets.Where(a => a.OperationalAreaId == query.OperationalAreaId);
        }
        if (query.MinLon is not null && query.MinLat is not null && query.MaxLon is not null && query.MaxLat is not null)
        {
            var envelope = new Envelope(query.MinLon.Value, query.MaxLon.Value, query.MinLat.Value, query.MaxLat.Value);
            var bbox = SpatialMath.Wgs84Factory.ToGeometry(envelope);
            bbox.SRID = SpatialMath.Wgs84;
            assets = assets.Where(a => a.Geometry != null && a.Geometry.Intersects(bbox));
        }

        var total = await assets.CountAsync(cancellationToken);
        var items = await assets.OrderBy(a => a.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AssetSummaryDto(
                a.Id, a.AssetCode, a.Name, a.AssetType, a.Status, a.Condition,
                a.Geometry == null ? null : a.Geometry.Centroid.X,
                a.Geometry == null ? null : a.Geometry.Centroid.Y,
                a.AdminArea == null ? null : a.AdminArea.Name,
                a.OperationalArea == null ? null : a.OperationalArea.Name,
                a.IsSensitive, a.DataClassification,
                a.DataSource == null ? null : a.DataSource.Name))
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetSummaryDto>(items, total, page, pageSize);
    }

    public async Task<AssetDetailDto?> GetAssetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.AsNoTracking()
            .Include(a => a.AdminArea).Include(a => a.OperationalArea).Include(a => a.DataSource)
            .Include(a => a.Documents).Include(a => a.Inspections).Include(a => a.MaintenanceRecords)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null || (asset.IsSensitive && !CanViewSensitive))
        {
            return null;
        }
        return ToDetail(asset);
    }

    public async Task<AssetDetailDto> CreateAssetAsync(AssetCreateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        if (await _db.Assets.AnyAsync(a => a.AssetCode == request.AssetCode, cancellationToken))
        {
            throw new InvalidOperationException($"An asset with code '{request.AssetCode}' already exists.");
        }

        var geometry = ResolveGeometry(request);
        var errors = _geoJson.Validate(geometry);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        var asset = new Asset
        {
            AssetCode = request.AssetCode.Trim(),
            Name = request.Name.Trim(),
            AssetType = request.AssetType,
            Description = request.Description,
            Geometry = geometry,
            AdminAreaId = request.AdminAreaId,
            OperationalAreaId = request.OperationalAreaId,
            Owner = request.Owner,
            Operator = request.Operator,
            InstalledOn = request.InstalledOn,
            Status = request.Status,
            Condition = request.Condition,
            CapacityM3PerDay = request.CapacityM3PerDay,
            SpecificationsJson = NormaliseJson(request.SpecificationsJson),
            DesignParametersJson = NormaliseJson(request.DesignParametersJson),
            IsSensitive = request.IsSensitive,
            SourceReference = request.SourceReference,
            DataClassification = DataClassification.Observed
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAssetAsync(asset.Id, cancellationToken))!;
    }

    public async Task<AssetDetailDto> UpdateAssetAsync(Guid id, AssetUpdateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Asset not found.");
        if (asset.IsSensitive && !CanViewSensitive)
        {
            throw new UnauthorizedAccessException("You are not permitted to edit sensitive infrastructure.");
        }
        if (await _db.Assets.AnyAsync(a => a.Id != id && a.AssetCode == request.AssetCode, cancellationToken))
        {
            throw new InvalidOperationException($"An asset with code '{request.AssetCode}' already exists.");
        }

        var geometry = ResolveGeometry(request);
        var errors = _geoJson.Validate(geometry);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        asset.AssetCode = request.AssetCode.Trim();
        asset.Name = request.Name.Trim();
        asset.AssetType = request.AssetType;
        asset.Description = request.Description;
        asset.Geometry = geometry;
        asset.AdminAreaId = request.AdminAreaId;
        asset.OperationalAreaId = request.OperationalAreaId;
        asset.Owner = request.Owner;
        asset.Operator = request.Operator;
        asset.InstalledOn = request.InstalledOn;
        asset.Status = request.Status;
        asset.Condition = request.Condition;
        asset.CapacityM3PerDay = request.CapacityM3PerDay;
        asset.SpecificationsJson = NormaliseJson(request.SpecificationsJson);
        asset.DesignParametersJson = NormaliseJson(request.DesignParametersJson);
        asset.IsSensitive = request.IsSensitive;
        asset.SourceReference = request.SourceReference;
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAssetAsync(asset.Id, cancellationToken))!;
    }

    public async Task<bool> DeleteAssetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return false;
        }
        if (asset.IsSensitive && !CanViewSensitive)
        {
            throw new UnauthorizedAccessException("You are not permitted to delete sensitive infrastructure.");
        }
        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AssetStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var query = _db.Assets.AsNoTracking();
        if (!CanViewSensitive)
        {
            query = query.Where(a => !a.IsSensitive);
        }
        var total = await query.CountAsync(cancellationToken);
        var byType = await query.GroupBy(a => a.AssetType)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count(), null)).ToListAsync(cancellationToken);
        var byStatus = await query.GroupBy(a => a.Status)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count(), null)).ToListAsync(cancellationToken);
        var byCondition = await query.GroupBy(a => a.Condition)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count(), null)).ToListAsync(cancellationToken);
        var withoutGeometry = await query.CountAsync(a => a.Geometry == null, cancellationToken);
        return new AssetStatisticsDto(total, byType, byStatus, byCondition, withoutGeometry);
    }

    public async Task<InspectionDto> AddInspectionAsync(Guid assetId, InspectionCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _db.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
        {
            throw new KeyNotFoundException("Asset not found.");
        }
        var inspection = new InspectionRecord
        {
            AssetId = assetId,
            ScheduledFor = request.ScheduledFor,
            CompletedAt = request.CompletedAt,
            InspectorName = request.InspectorName,
            Findings = request.Findings,
            ConditionAfter = request.ConditionAfter,
            DataClassification = DataClassification.Observed
        };
        _db.Inspections.Add(inspection);
        await _db.SaveChangesAsync(cancellationToken);
        return new InspectionDto(inspection.Id, inspection.ScheduledFor, inspection.CompletedAt,
            inspection.InspectorName, inspection.Findings, inspection.ConditionAfter);
    }

    public async Task<MaintenanceDto> AddMaintenanceAsync(Guid assetId, MaintenanceCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _db.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
        {
            throw new KeyNotFoundException("Asset not found.");
        }
        var record = new MaintenanceRecord
        {
            AssetId = assetId,
            Type = request.Type,
            PerformedAt = request.PerformedAt,
            PerformedBy = request.PerformedBy,
            Description = request.Description,
            Cost = request.Cost,
            WorkOrderId = request.WorkOrderId,
            DataClassification = DataClassification.Observed
        };
        _db.MaintenanceRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return new MaintenanceDto(record.Id, record.Type, record.PerformedAt, record.PerformedBy, record.Description, record.Cost);
    }

    private Geometry ResolveGeometry(AssetCreateRequest request)
    {
        Geometry? geometry = null;
        if (!string.IsNullOrWhiteSpace(request.GeometryGeoJson))
        {
            geometry = _geoJson.ParseGeometry(request.GeometryGeoJson);
        }
        else if (request.Longitude is not null && request.Latitude is not null)
        {
            geometry = SpatialMath.Point(request.Longitude.Value, request.Latitude.Value);
        }

        if (geometry is null)
        {
            throw new ArgumentException("A latitude/longitude pair or a valid GeoJSON geometry is required.");
        }

        if (request.AssetType is AssetType.DistributionMain or AssetType.TransmissionMain &&
            geometry is not LineString)
        {
            throw new ArgumentException("Distribution and transmission mains must use a LineString geometry.");
        }
        if (request.AssetType is not (AssetType.DistributionMain or AssetType.TransmissionMain) &&
            geometry is LineString or Polygon or MultiPolygon)
        {
            throw new ArgumentException("Point assets must use a Point geometry. Use a pipeline asset type for lines.");
        }

        geometry.SRID = SpatialMath.Wgs84;
        return geometry;
    }

    private AssetDetailDto ToDetail(Asset asset)
    {
        var centroid = asset.Geometry?.Centroid;
        return new AssetDetailDto(
            asset.Id, asset.AssetCode, asset.Name, asset.AssetType, asset.Description,
            asset.Status, asset.Condition, _geoJson.SerializeGeometry(asset.Geometry),
            centroid?.X, centroid?.Y,
            asset.AdminAreaId, asset.AdminArea?.Name,
            asset.OperationalAreaId, asset.OperationalArea?.Name,
            asset.Owner, asset.Operator, asset.InstalledOn, asset.CapacityM3PerDay,
            asset.SpecificationsJson, asset.DesignParametersJson, asset.IsSensitive,
            asset.DataClassification, asset.SourceReference,
            asset.DataSource is null ? null : new SourceRefDto(asset.DataSource.Id, asset.DataSource.Name,
                asset.DataSource.Licence, asset.DataSource.SourceUrl),
            asset.CreatedAt, asset.UpdatedAt,
            asset.Documents.Select(d => new AssetDocumentDto(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Caption, d.CreatedAt)).ToList(),
            asset.Inspections.OrderByDescending(i => i.ScheduledFor).Select(i => new InspectionDto(i.Id, i.ScheduledFor, i.CompletedAt, i.InspectorName, i.Findings, i.ConditionAfter)).ToList(),
            asset.MaintenanceRecords.OrderByDescending(m => m.PerformedAt).Select(m => new MaintenanceDto(m.Id, m.Type, m.PerformedAt, m.PerformedBy, m.Description, m.Cost)).ToList());
    }

    private static string? NormaliseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(json);
            return json;
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Specifications/design parameters must be valid JSON.");
        }
    }
}
