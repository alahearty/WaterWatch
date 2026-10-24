using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IHydrologyService
{
    Task<IReadOnlyList<HydrologyStationDto>> GetStationsAsync(CancellationToken cancellationToken = default);
    Task<HydrologyStationDto> CreateStationAsync(HydrologyStationCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HydrologyObservationDto>> GetObservationsAsync(HydrologyQuery query, CancellationToken cancellationToken = default);
    Task<HydrologyObservationDto> AddObservationAsync(HydrologyObservationCreateRequest request, CancellationToken cancellationToken = default);
    Task<HydrologySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<RainfallLiveDto> GetLiveRainfallAsync(CancellationToken cancellationToken = default);
    Task<int> SyncRainfallAsync(int days, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DrainageObstructionDto>> GetObstructionsAsync(CancellationToken cancellationToken = default);
    Task<DrainageObstructionDto> CreateObstructionAsync(DrainageObstructionCreateRequest request, string userName, CancellationToken cancellationToken = default);
}

public class HydrologyService : IHydrologyService
{
    private readonly DatabaseContext _db;
    private readonly IWeatherService _weather;

    public HydrologyService(DatabaseContext db, IWeatherService weather)
    {
        _db = db;
        _weather = weather;
    }

    public async Task<IReadOnlyList<HydrologyStationDto>> GetStationsAsync(CancellationToken cancellationToken = default) =>
        await _db.HydrologyStations.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new HydrologyStationDto(s.Id, s.Code, s.Name, s.StationType,
                s.Geometry == null ? null : s.Geometry.X, s.Geometry == null ? null : s.Geometry.Y,
                s.WaterwayName, s.Datum, s.IsActive, s.DataClassification,
                s.Observations.OrderByDescending(o => o.ObservedAt).Select(o => (DateTimeOffset?)o.ObservedAt).FirstOrDefault(),
                s.Observations.Count))
            .ToListAsync(cancellationToken);

    public async Task<HydrologyStationDto> CreateStationAsync(HydrologyStationCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.HydrologyStations.AnyAsync(s => s.Code == request.Code, cancellationToken))
        {
            throw new InvalidOperationException($"A hydrology station with code '{request.Code}' already exists.");
        }
        if (request.Latitude is null || request.Longitude is null)
        {
            throw new ArgumentException("Hydrology stations require a latitude and longitude.");
        }
        var station = new HydrologyStation
        {
            Code = request.Code.Trim(), Name = request.Name.Trim(), StationType = request.StationType,
            Geometry = SpatialMath.Point(request.Longitude.Value, request.Latitude.Value),
            WaterwayName = request.WaterwayName, AdminAreaId = request.AdminAreaId, Datum = request.Datum,
            Notes = request.Notes, DataClassification = DataClassification.Observed
        };
        _db.HydrologyStations.Add(station);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetStationsAsync(cancellationToken)).First(s => s.Id == station.Id);
    }

    public async Task<IReadOnlyList<HydrologyObservationDto>> GetObservationsAsync(HydrologyQuery query, CancellationToken cancellationToken = default)
    {
        var observations = _db.HydrologyObservations.AsNoTracking()
            .Include(o => o.Station).Include(o => o.DataSource).AsQueryable();
        if (query.StationId is not null)
        {
            observations = observations.Where(o => o.StationId == query.StationId);
        }
        if (query.Parameter is not null)
        {
            observations = observations.Where(o => o.Parameter == query.Parameter);
        }
        if (query.From is not null)
        {
            var from = new DateTimeOffset(query.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(1));
            observations = observations.Where(o => o.ObservedAt >= from);
        }
        if (query.To is not null)
        {
            var to = new DateTimeOffset(query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(1));
            observations = observations.Where(o => o.ObservedAt < to);
        }
        return await observations.OrderByDescending(o => o.ObservedAt).Take(Math.Clamp(query.Limit, 1, 5000))
            .Select(o => new HydrologyObservationDto(o.Id, o.StationId, o.Station.Name, o.ObservedAt,
                o.Parameter, o.Value, o.Unit, o.QualityFlag, o.DataClassification,
                o.DataSource == null ? null : o.DataSource.Name, o.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<HydrologyObservationDto> AddObservationAsync(HydrologyObservationCreateRequest request, CancellationToken cancellationToken = default)
    {
        var station = await _db.HydrologyStations.FirstOrDefaultAsync(s => s.Id == request.StationId, cancellationToken)
            ?? throw new KeyNotFoundException("Hydrology station not found.");
        var duplicate = await _db.HydrologyObservations.AnyAsync(o =>
            o.StationId == request.StationId && o.Parameter == request.Parameter && o.ObservedAt == request.ObservedAt, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("An observation for this station, parameter and time already exists.");
        }
        var observation = new HydrologyObservation
        {
            StationId = request.StationId, ObservedAt = request.ObservedAt, Parameter = request.Parameter,
            Value = request.Value, Unit = request.Unit, QualityFlag = request.QualityFlag,
            Notes = request.Notes, DataClassification = DataClassification.Observed
        };
        _db.HydrologyObservations.Add(observation);
        await _db.SaveChangesAsync(cancellationToken);
        return new HydrologyObservationDto(observation.Id, station.Id, station.Name, observation.ObservedAt,
            observation.Parameter, observation.Value, observation.Unit, observation.QualityFlag,
            observation.DataClassification, null, observation.Notes);
    }

    public async Task<HydrologySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var stations = await _db.HydrologyStations.CountAsync(cancellationToken);
        var rainfall = await _db.HydrologyStations.CountAsync(s => s.StationType == HydrologyStationType.Rainfall, cancellationToken);
        var waterLevel = await _db.HydrologyStations.CountAsync(s => s.StationType == HydrologyStationType.WaterLevel, cancellationToken);
        var observations = await _db.HydrologyObservations.CountAsync(cancellationToken);
        var last = await _db.HydrologyObservations.OrderByDescending(o => o.ObservedAt)
            .Select(o => (DateTimeOffset?)o.ObservedAt).FirstOrDefaultAsync(cancellationToken);
        var waterway = await _db.SpatialFeatures.CountAsync(f => f.Layer.Key == "osm-waterways", cancellationToken);
        var waterBody = await _db.SpatialFeatures.CountAsync(f => f.Layer.Key == "osm-water-bodies", cancellationToken);
        var message = observations == 0
            ? "No hydrology observations have been recorded. Rainfall can be synced from Open-Meteo; water level and flow require a gauge or operator import."
            : "Waterway geometry is mapped OSM centrelines. Observations are shown separately and are not a hydrodynamic model.";
        return new HydrologySummaryDto(stations, rainfall, waterLevel, observations, waterway, waterBody, last, message);
    }

    public Task<RainfallLiveDto> GetLiveRainfallAsync(CancellationToken cancellationToken = default) =>
        _weather.GetLiveAsync(4.8156, 7.0498, cancellationToken);

    public async Task<int> SyncRainfallAsync(int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-days + 1);
        var rainfall = await _weather.GetArchiveDailyRainfallAsync(from, to, cancellationToken);
        if (rainfall.Count == 0)
        {
            return 0;
        }

        var source = await _db.DataSources.FirstOrDefaultAsync(d => d.Name == "Open-Meteo", cancellationToken);
        if (source is null)
        {
            source = new DataSource
            {
                Name = "Open-Meteo", Publisher = "Open-Meteo", Licence = "CC BY 4.0",
                LicenceUrl = "https://open-meteo.com/en/license",
                SourceUrl = "https://open-meteo.com/",
                AcquiredAt = DateTimeOffset.UtcNow,
                Coverage = "Port Harcourt (4.8156, 7.0498)",
                QualityNotes = "Gridded weather forecast/reanalysis; not a local rain-gauge observation.",
                UpdateFrequency = "Hourly", IsOfficial = false, DataClassification = DataClassification.Imported
            };
            _db.DataSources.Add(source);
        }

        var station = await _db.HydrologyStations.FirstOrDefaultAsync(s => s.Code == "OM-PHC-RAIN", cancellationToken);
        if (station is null)
        {
            station = new HydrologyStation
            {
                Code = "OM-PHC-RAIN", Name = "Port Harcourt (Open-Meteo grid point)",
                StationType = HydrologyStationType.Rainfall, Geometry = SpatialMath.Point(7.0498, 4.8156),
                Datum = "Gridded reanalysis", IsActive = true, DataSource = source,
                Notes = "Virtual station for Open-Meteo gridded rainfall. Not a physical rain gauge.",
                DataClassification = DataClassification.Imported
            };
            _db.HydrologyStations.Add(station);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var existing = await _db.HydrologyObservations
            .Where(o => o.StationId == station.Id && o.Parameter == HydrologyParameter.RainfallMm)
            .ToDictionaryAsync(o => DateOnly.FromDateTime(o.ObservedAt.Date), cancellationToken);
        var inserted = 0;
        foreach (var point in rainfall)
        {
            if (!DateOnly.TryParseExact(point.Date, "yyyy-MM-dd", out var date))
            {
                continue;
            }
            var observedAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(1));
            if (existing.TryGetValue(date, out var observation))
            {
                observation.Value = point.RainfallMm;
                observation.Notes = "Open-Meteo ERA5 archive daily precipitation; provisional.";
                observation.DataClassification = DataClassification.Imported;
            }
            else
            {
                _db.HydrologyObservations.Add(new HydrologyObservation
                {
                    StationId = station.Id, ObservedAt = observedAt, Parameter = HydrologyParameter.RainfallMm,
                    Value = point.RainfallMm, Unit = "mm", QualityFlag = QualityFlag.Provisional,
                    DataSource = source, DataClassification = DataClassification.Imported,
                    Notes = "Open-Meteo ERA5 archive daily precipitation; provisional."
                });
                inserted++;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return inserted;
    }

    public async Task<IReadOnlyList<DrainageObstructionDto>> GetObstructionsAsync(CancellationToken cancellationToken = default) =>
        await _db.DrainageObstructions.AsNoTracking().Include(o => o.AdminArea)
            .OrderByDescending(o => o.ReportedAt)
            .Select(o => new DrainageObstructionDto(o.Id, o.Reference, o.ReportedAt,
                o.Geometry == null ? null : o.Geometry.X, o.Geometry == null ? null : o.Geometry.Y,
                o.WaterwayName, o.Description, o.Severity, o.Status, o.ReportedBy,
                o.AdminArea == null ? null : o.AdminArea.Name, o.DataClassification))
            .ToListAsync(cancellationToken);

    public async Task<DrainageObstructionDto> CreateObstructionAsync(DrainageObstructionCreateRequest request, string userName, CancellationToken cancellationToken = default)
    {
        var reference = $"DRAIN-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var obstruction = new DrainageObstruction
        {
            Reference = reference, ReportedAt = DateTimeOffset.UtcNow,
            Geometry = request.Longitude is not null && request.Latitude is not null
                ? SpatialMath.Point(request.Longitude.Value, request.Latitude.Value) : null,
            WaterwayName = request.WaterwayName, Description = request.Description,
            Severity = request.Severity, Status = "Reported", ReportedBy = userName,
            AdminAreaId = request.AdminAreaId, DataClassification = DataClassification.Observed
        };
        _db.DrainageObstructions.Add(obstruction);
        await _db.SaveChangesAsync(cancellationToken);
        return new DrainageObstructionDto(obstruction.Id, obstruction.Reference, obstruction.ReportedAt,
            obstruction.Geometry?.X, obstruction.Geometry?.Y, obstruction.WaterwayName,
            obstruction.Description, obstruction.Severity, obstruction.Status, obstruction.ReportedBy,
            null, obstruction.DataClassification);
    }
}
