using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Realtime;

namespace WaterWatch.Services;

public interface ITelemetryService
{
    Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<DeviceCreatedDto> CreateDeviceAsync(DeviceCreateRequest request, CancellationToken cancellationToken = default);
    Task<TelemetryReadingDto> IngestAsync(TelemetryIngestRequest request, string? apiKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetryReadingDto>> GetReadingsAsync(TelemetryQuery query, CancellationToken cancellationToken = default);
    Task<TelemetrySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRuleDto>> GetAlertRulesAsync(CancellationToken cancellationToken = default);
    Task<AlertRuleDto> CreateAlertRuleAsync(AlertRuleCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default);
    Task<AlertDto> AcknowledgeAlertAsync(Guid alertId, string userName, string? notes, CancellationToken cancellationToken = default);
    Task<int> EscalateDueAlertsAsync(CancellationToken cancellationToken = default);
    Task<TelemetryReadingDto?> GenerateDemoReadingAsync(CancellationToken cancellationToken = default);
}

public class TelemetryService : ITelemetryService
{
    private readonly DatabaseContext _db;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly IConfiguration _configuration;

    public TelemetryService(DatabaseContext db, IHubContext<TelemetryHub> hub, IConfiguration configuration)
    {
        _db = db;
        _hub = hub;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        await RefreshDeviceStatusesAsync(cancellationToken);
        var devices = await _db.Devices.AsNoTracking().Include(d => d.Asset).OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
        var latest = await GetLatestReadingsAsync(cancellationToken);
        return devices.Select(d =>
        {
            latest.TryGetValue(d.Id, out var reading);
            var offline = d.Status == DeviceStatus.Offline ||
                          (d.LastSeenAt is not null && d.LastSeenAt < DateTimeOffset.UtcNow.AddMinutes(-d.OfflineThresholdMinutes));
            var freshness = d.LastSeenAt is null ? "No data"
                : offline ? "Offline"
                : d.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-5) ? "Live"
                : "Recent";
            return new DeviceDto(d.Id, d.DeviceCode, d.Name, d.DeviceType, d.Status, d.AssetId,
                d.Asset?.Name, d.Geometry?.X, d.Geometry?.Y, d.LastSeenAt,
                reading?.Parameter, reading?.Value, reading?.Unit, offline,
                d.OfflineThresholdMinutes, d.IsDemoDevice, d.DataClassification, freshness);
        }).ToList();
    }

    public async Task<DeviceCreatedDto> CreateDeviceAsync(DeviceCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.Devices.AnyAsync(d => d.DeviceCode == request.DeviceCode, cancellationToken))
        {
            throw new InvalidOperationException($"A device with code '{request.DeviceCode}' already exists.");
        }
        var apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var device = new Device
        {
            DeviceCode = request.DeviceCode.Trim(), Name = request.Name.Trim(), DeviceType = request.DeviceType,
            AssetId = request.AssetId, HydrologyStationId = request.HydrologyStationId,
            Geometry = request.Longitude is not null && request.Latitude is not null
                ? SpatialMath.Point(request.Longitude.Value, request.Latitude.Value) : null,
            Status = DeviceStatus.Active, FirmwareVersion = request.FirmwareVersion,
            InstalledOn = request.InstalledOn, ApiKeyHash = HashKey(apiKey),
            OfflineThresholdMinutes = Math.Clamp(request.OfflineThresholdMinutes, 10, 10080),
            IsDemoDevice = request.IsDemoDevice, DataClassification = DataClassification.Observed
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(cancellationToken);
        return new DeviceCreatedDto(ToDeviceDto(device, null), apiKey,
            "Store this key securely. It is shown once and is required in the X-Device-Key header for ingestion.");
    }

    public async Task<TelemetryReadingDto> IngestAsync(TelemetryIngestRequest request, string? apiKey, CancellationToken cancellationToken = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceCode == request.DeviceCode, cancellationToken)
            ?? throw new UnauthorizedAccessException("Unknown device code.");
        if (device.Status == DeviceStatus.Retired)
        {
            throw new InvalidOperationException("The device is retired and cannot submit telemetry.");
        }
        if (device.ApiKeyHash is null || string.IsNullOrWhiteSpace(apiKey) ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(device.ApiKeyHash), Convert.FromHexString(HashKey(apiKey))))
        {
            throw new UnauthorizedAccessException("Invalid device key.");
        }

        return await PersistReadingAsync(device, request, request.Quality, cancellationToken);
    }

    private async Task<TelemetryReadingDto> PersistReadingAsync(Device device, TelemetryIngestRequest request, TelemetryQuality quality, CancellationToken cancellationToken)
    {
        var observedAt = request.ObservedAt ?? DateTimeOffset.UtcNow;
        if (observedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new ArgumentException("Observation timestamp is more than 5 minutes in the future.");
        }
        if (observedAt < DateTimeOffset.UtcNow.AddDays(-365))
        {
            throw new ArgumentException("Observation timestamp is older than the 365-day ingestion window.");
        }
        if (request.Value is > 1_000_000_000m or < -1_000_000_000m)
        {
            throw new ArgumentException("Telemetry value is outside the accepted numeric range.");
        }
        if (string.IsNullOrWhiteSpace(request.Parameter) || string.IsNullOrWhiteSpace(request.Unit))
        {
            throw new ArgumentException("Parameter and unit are required.");
        }
        if (await _db.TelemetryReadings.AnyAsync(r => r.DeviceId == device.Id && r.ObservedAt == observedAt && r.Parameter == request.Parameter, cancellationToken))
        {
            throw new InvalidOperationException("A reading for this device, parameter and timestamp already exists.");
        }

        var classification = quality == TelemetryQuality.SyntheticDemonstration
            ? DataClassification.SyntheticDemonstration
            : quality == TelemetryQuality.Estimated ? DataClassification.Estimated : DataClassification.Observed;
        var reading = new TelemetryReading
        {
            DeviceId = device.Id, ObservedAt = observedAt, Parameter = request.Parameter.Trim(),
            Value = request.Value, Unit = request.Unit.Trim(), Quality = quality,
            DataClassification = classification, RawPayloadJson = request.RawPayloadJson,
            ReceivedAt = DateTimeOffset.UtcNow
        };
        _db.TelemetryReadings.Add(reading);
        device.LastSeenAt = observedAt;
        if (device.Status is DeviceStatus.Offline or DeviceStatus.Active)
        {
            device.Status = DeviceStatus.Active;
        }
        var alerts = await EvaluateRulesAsync(device, reading, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new TelemetryReadingDto(reading.Id, device.Id, device.DeviceCode, device.Name,
            reading.ObservedAt, reading.Parameter, reading.Value, reading.Unit, reading.Quality,
            reading.DataClassification, classification == DataClassification.SyntheticDemonstration, reading.ReceivedAt);
        await _hub.Clients.All.SendAsync("telemetry", dto, cancellationToken);
        foreach (var alert in alerts)
        {
            await _hub.Clients.All.SendAsync("alert", ToAlertDto(alert, device), cancellationToken);
        }
        return dto;
    }

    private async Task<List<Alert>> EvaluateRulesAsync(Device device, TelemetryReading reading, CancellationToken cancellationToken)
    {
        var rules = await _db.AlertRules
            .Where(r => r.IsActive && (r.DeviceType == null || r.DeviceType == device.DeviceType))
            .ToListAsync(cancellationToken);
        var raised = new List<Alert>();
        foreach (var rule in rules.Where(r => string.Equals(r.Parameter, reading.Parameter, StringComparison.OrdinalIgnoreCase)))
        {
            var triggered = Triggered(rule, reading.Value);
            var existing = await _db.Alerts
                .FirstOrDefaultAsync(a => a.AlertRuleId == rule.Id && a.DeviceId == device.Id &&
                                          (a.Status == AlertStatus.Open || a.Status == AlertStatus.Acknowledged), cancellationToken);
            if (triggered && existing is null)
            {
                var alert = new Alert
                {
                    AlertRuleId = rule.Id, AlertRule = rule, DeviceId = device.Id,
                    TriggeredAt = DateTimeOffset.UtcNow,
                    Value = reading.Value, Message = $"{reading.Parameter} = {reading.Value} {reading.Unit} breached rule '{rule.Name}'.",
                    Status = AlertStatus.Open, DataClassification = reading.DataClassification
                };
                _db.Alerts.Add(alert);
                raised.Add(alert);
            }
            else if (!triggered && existing is not null)
            {
                existing.Status = AlertStatus.Resolved;
                existing.ResolvedAt = DateTimeOffset.UtcNow;
                existing.Notes = "Automatically resolved when a subsequent reading returned within the rule threshold.";
            }
        }
        return raised;
    }

    private static bool Triggered(AlertRule rule, decimal value) => rule.Operator switch
    {
        AlertOperator.GreaterThan => rule.Threshold is not null && value > rule.Threshold,
        AlertOperator.LessThan => rule.Threshold is not null && value < rule.Threshold,
        AlertOperator.OutsideRange => rule.Threshold is not null && rule.Threshold2 is not null &&
                                      (value < rule.Threshold || value > rule.Threshold2),
        _ => false
    };

    public async Task<IReadOnlyList<TelemetryReadingDto>> GetReadingsAsync(TelemetryQuery query, CancellationToken cancellationToken = default)
    {
        var readings = _db.TelemetryReadings.AsNoTracking().Include(r => r.Device).AsQueryable();
        if (query.DeviceId is not null)
        {
            readings = readings.Where(r => r.DeviceId == query.DeviceId);
        }
        if (query.From is not null)
        {
            readings = readings.Where(r => r.ObservedAt >= query.From);
        }
        if (query.To is not null)
        {
            readings = readings.Where(r => r.ObservedAt <= query.To);
        }
        return await readings.OrderByDescending(r => r.ObservedAt).Take(Math.Clamp(query.Limit, 1, 10000))
            .Select(r => new TelemetryReadingDto(r.Id, r.DeviceId, r.Device.DeviceCode, r.Device.Name,
                r.ObservedAt, r.Parameter, r.Value, r.Unit, r.Quality, r.DataClassification,
                r.DataClassification == DataClassification.SyntheticDemonstration, r.ReceivedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TelemetrySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await RefreshDeviceStatusesAsync(cancellationToken);
        var devices = await _db.Devices.AsNoTracking().ToListAsync(cancellationToken);
        var readings = await _db.TelemetryReadings.CountAsync(cancellationToken);
        var openAlerts = await _db.Alerts.CountAsync(a => a.Status != AlertStatus.Resolved, cancellationToken);
        var last = await _db.TelemetryReadings.OrderByDescending(r => r.ObservedAt)
            .Select(r => (DateTimeOffset?)r.ObservedAt).FirstOrDefaultAsync(cancellationToken);
        var demoMode = _configuration.GetValue("WaterWatch:DemoMode", false);
        var message = devices.Count == 0
            ? "No monitoring devices are registered. Register a device and ingest telemetry with its API key."
            : readings == 0
                ? "Devices are registered but no telemetry has been received."
                : demoMode
                    ? "Demonstration telemetry is enabled and every synthetic reading is labelled. It is not mixed silently with observed telemetry."
                    : "Telemetry is received through the authenticated ingestion API.";
        return new TelemetrySummaryDto(devices.Count,
            devices.Count(d => d.Status == DeviceStatus.Active), devices.Count(d => d.Status == DeviceStatus.Offline),
            devices.Count(d => d.IsDemoDevice), readings, openAlerts, last, message, demoMode);
    }

    public async Task<IReadOnlyList<AlertRuleDto>> GetAlertRulesAsync(CancellationToken cancellationToken = default) =>
        await _db.AlertRules.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new AlertRuleDto(r.Id, r.Name, r.DeviceType, r.Parameter, r.Operator,
                r.Threshold, r.Threshold2, r.Severity, r.IsActive, r.EscalationMinutes, r.Description))
            .ToListAsync(cancellationToken);

    public async Task<AlertRuleDto> CreateAlertRuleAsync(AlertRuleCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Operator == AlertOperator.OutsideRange && (request.Threshold is null || request.Threshold2 is null))
        {
            throw new ArgumentException("Outside-range rules require both threshold values.");
        }
        var rule = new AlertRule
        {
            Name = request.Name.Trim(), DeviceType = request.DeviceType, Parameter = request.Parameter.Trim(),
            Operator = request.Operator, Threshold = request.Threshold, Threshold2 = request.Threshold2,
            Severity = request.Severity, EscalationMinutes = Math.Clamp(request.EscalationMinutes, 5, 10080),
            Description = request.Description, IsActive = true, DataClassification = DataClassification.Observed
        };
        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return new AlertRuleDto(rule.Id, rule.Name, rule.DeviceType, rule.Parameter, rule.Operator,
            rule.Threshold, rule.Threshold2, rule.Severity, rule.IsActive, rule.EscalationMinutes, rule.Description);
    }

    public async Task<IReadOnlyList<AlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var alerts = await _db.Alerts.AsNoTracking().Include(a => a.AlertRule).Include(a => a.Device)
            .OrderByDescending(a => a.TriggeredAt).Take(500).ToListAsync(cancellationToken);
        return alerts.Select(a => ToAlertDto(a, a.Device)).ToList();
    }

    public async Task<AlertDto> AcknowledgeAlertAsync(Guid alertId, string userName, string? notes, CancellationToken cancellationToken = default)
    {
        var alert = await _db.Alerts.Include(a => a.AlertRule).Include(a => a.Device)
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken)
            ?? throw new KeyNotFoundException("Alert not found.");
        if (alert.Status != AlertStatus.Open)
        {
            throw new InvalidOperationException("Only open alerts can be acknowledged.");
        }
        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedBy = userName;
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.Notes = notes;
        await _db.SaveChangesAsync(cancellationToken);
        return ToAlertDto(alert, alert.Device);
    }

    public async Task<int> EscalateDueAlertsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var alerts = await _db.Alerts.Include(a => a.AlertRule).Include(a => a.Device)
            .Where(a => a.Status == AlertStatus.Open).ToListAsync(cancellationToken);
        var escalated = 0;
        foreach (var alert in alerts.Where(a => a.TriggeredAt.AddMinutes(a.AlertRule.EscalationMinutes) <= now))
        {
            alert.EscalatedAt = now;
            alert.EscalationCount++;
            alert.Notes = $"Escalated at {now:u} after {alert.AlertRule.EscalationMinutes} minutes without acknowledgement.";
            escalated++;
        }
        if (escalated > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var alert in alerts.Where(a => a.EscalatedAt == now))
            {
                await _hub.Clients.All.SendAsync("alertEscalated", ToAlertDto(alert, alert.Device), cancellationToken);
            }
        }
        return escalated;
    }

    public async Task<TelemetryReadingDto?> GenerateDemoReadingAsync(CancellationToken cancellationToken = default)
    {
        var device = await _db.Devices.Where(d => d.IsDemoDevice && d.Status != DeviceStatus.Retired)
            .OrderBy(d => d.DeviceCode).FirstOrDefaultAsync(cancellationToken);
        if (device is null)
        {
            return null;
        }
        var (parameter, unit, value) = device.DeviceType switch
        {
            DeviceType.Pressure => ("pressure_bar", "bar", 2.2m + (decimal)(Random.Shared.NextDouble() - 0.5) * 0.8m),
            DeviceType.ReservoirLevel => ("level_m", "m", 3.6m + (decimal)(Random.Shared.NextDouble() - 0.5) * 0.5m),
            DeviceType.WaterLevel => ("level_m", "m", 2.4m + (decimal)(Random.Shared.NextDouble() - 0.5) * 0.3m),
            DeviceType.Flow => ("flow_m3s", "m3/s", 0.18m + (decimal)Random.Shared.NextDouble() * 0.12m),
            DeviceType.WaterQuality => ("turbidity_ntu", "NTU", 2.5m + (decimal)Random.Shared.NextDouble() * 4m),
            DeviceType.RainGauge => ("rainfall_mm", "mm", Math.Round((decimal)Random.Shared.NextDouble() * 8m, 2)),
            _ => ("pump_current_a", "A", 12m + (decimal)Random.Shared.NextDouble() * 3m)
        };
        var request = new TelemetryIngestRequest
        {
            DeviceCode = device.DeviceCode, ObservedAt = DateTimeOffset.UtcNow,
            Parameter = parameter, Value = Math.Round(value, 3), Unit = unit,
            Quality = TelemetryQuality.SyntheticDemonstration,
            RawPayloadJson = "{\"source\":\"WaterWatch demonstration telemetry\"}"
        };
        return await PersistReadingAsync(device, request, TelemetryQuality.SyntheticDemonstration, cancellationToken);
    }

    private async Task RefreshDeviceStatusesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var devices = await _db.Devices.Where(d => d.Status != DeviceStatus.Retired && d.Status != DeviceStatus.Maintenance)
            .ToListAsync(cancellationToken);
        var changed = false;
        foreach (var device in devices)
        {
            var offline = device.LastSeenAt is null || device.LastSeenAt < now.AddMinutes(-device.OfflineThresholdMinutes);
            var status = offline ? DeviceStatus.Offline : DeviceStatus.Active;
            if (device.Status != status)
            {
                device.Status = status;
                changed = true;
            }
        }
        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, TelemetryReading>> GetLatestReadingsAsync(CancellationToken cancellationToken)
    {
        var readings = await _db.TelemetryReadings.AsNoTracking()
            .OrderByDescending(r => r.ObservedAt).Take(5000).ToListAsync(cancellationToken);
        return readings.GroupBy(r => r.DeviceId).ToDictionary(g => g.Key, g => g.First());
    }

    private AlertDto ToAlertDto(Alert alert, Device device) => new(
        alert.Id, alert.AlertRuleId, alert.AlertRule.Name, alert.DeviceId, device.Name, device.DeviceCode,
        alert.TriggeredAt, alert.Value, alert.Message, alert.Status, alert.AlertRule.Severity,
        alert.AcknowledgedBy, alert.AcknowledgedAt, alert.ResolvedAt,
        alert.Status == AlertStatus.Open && alert.TriggeredAt.AddMinutes(alert.AlertRule.EscalationMinutes) <= DateTimeOffset.UtcNow,
        alert.EscalatedAt, alert.EscalationCount, alert.DataClassification);

    private static DeviceDto ToDeviceDto(Device d, TelemetryReading? reading) => new(
        d.Id, d.DeviceCode, d.Name, d.DeviceType, d.Status, d.AssetId, d.Asset?.Name,
        d.Geometry?.X, d.Geometry?.Y, d.LastSeenAt, reading?.Parameter, reading?.Value, reading?.Unit,
        d.Status == DeviceStatus.Offline, d.OfflineThresholdMinutes, d.IsDemoDevice,
        d.DataClassification, d.LastSeenAt is null ? "No data" : "Awaiting first reading");

    private static string HashKey(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}
