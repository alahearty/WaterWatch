using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IWeatherService
{
    Task<RainfallLiveDto> GetLiveAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RainfallPointDto>> GetArchiveDailyRainfallAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public class OpenMeteoWeatherService : IWeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public OpenMeteoWeatherService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<RainfallLiveDto> GetLiveAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"open-meteo-live:{latitude:F4}:{longitude:F4}";
        if (_cache.TryGetValue(cacheKey, out RainfallLiveDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("open-meteo");
            var url = $"/v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                      $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                      "&current=temperature_2m,precipitation,rain" +
                      "&daily=precipitation_sum" +
                      "&past_days=7&forecast_days=7&timezone=Africa%2FLagos";
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var daily = new List<RainfallPointDto>();
            if (root.TryGetProperty("daily", out var dailyElement) &&
                dailyElement.TryGetProperty("time", out var times) &&
                dailyElement.TryGetProperty("precipitation_sum", out var values))
            {
                var timeArray = times.EnumerateArray().ToList();
                var valueArray = values.EnumerateArray().ToList();
                for (var i = 0; i < Math.Min(timeArray.Count, valueArray.Count); i++)
                {
                    daily.Add(new RainfallPointDto(timeArray[i].GetString() ?? string.Empty,
                        valueArray[i].ValueKind == JsonValueKind.Number ? valueArray[i].GetDecimal() : 0,
                        "Open-Meteo forecast / reanalysis", "Imported"));
                }
            }

            string? temperature = null;
            string? precipitation = null;
            if (root.TryGetProperty("current", out var current))
            {
                if (current.TryGetProperty("temperature_2m", out var t) && t.ValueKind == JsonValueKind.Number)
                {
                    temperature = t.GetDecimal().ToString(CultureInfo.InvariantCulture);
                }
                if (current.TryGetProperty("precipitation", out var p) && p.ValueKind == JsonValueKind.Number)
                {
                    precipitation = p.GetDecimal().ToString(CultureInfo.InvariantCulture);
                }
            }

            var result = new RainfallLiveDto(latitude, longitude, "Africa/Lagos", DateTimeOffset.UtcNow,
                daily, "Open-Meteo", "CC BY 4.0 (Open-Meteo)", temperature, precipitation,
                "available", "Daily precipitation is forecast/reanalysis, not a local rain-gauge observation.");
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));
            return result;
        }
        catch (Exception ex)
        {
            return new RainfallLiveDto(latitude, longitude, "Africa/Lagos", DateTimeOffset.UtcNow,
                Array.Empty<RainfallPointDto>(), "Open-Meteo", "CC BY 4.0 (Open-Meteo)",
                null, null, "unavailable", $"Open-Meteo could not be reached: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<RainfallPointDto>> GetArchiveDailyRainfallAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var key = $"open-meteo-archive:{from:yyyyMMdd}:{to:yyyyMMdd}";
        if (_cache.TryGetValue(key, out IReadOnlyList<RainfallPointDto>? cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient("open-meteo-archive");
        var url = $"/v1/archive?latitude=4.8156&longitude=7.0498&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}" +
                  "&daily=precipitation_sum&timezone=Africa%2FLagos";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var results = new List<RainfallPointDto>();
        if (root.TryGetProperty("daily", out var daily) &&
            daily.TryGetProperty("time", out var times) &&
            daily.TryGetProperty("precipitation_sum", out var values))
        {
            var timeArray = times.EnumerateArray().ToList();
            var valueArray = values.EnumerateArray().ToList();
            for (var i = 0; i < Math.Min(timeArray.Count, valueArray.Count); i++)
            {
                results.Add(new RainfallPointDto(timeArray[i].GetString() ?? string.Empty,
                    valueArray[i].ValueKind == JsonValueKind.Number ? valueArray[i].GetDecimal() : 0,
                    "Open-Meteo ERA5 archive", "Imported"));
            }
        }

        _cache.Set(key, results, TimeSpan.FromHours(6));
        return results;
    }
}
