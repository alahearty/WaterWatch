using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/geography")]
public class GeographyApiController : ControllerBase
{
    private readonly IGeographyService _geography;

    public GeographyApiController(IGeographyService geography) => _geography = geography;

    [HttpGet("context")]
    public async Task<ActionResult<GeographyContextDto>> Context(CancellationToken cancellationToken) =>
        Ok(await _geography.GetContextAsync(cancellationToken));

    [HttpGet("admin-areas")]
    public async Task<ActionResult<IReadOnlyList<AdminAreaDto>>> AdminAreas([FromQuery] AdminLevel? level, CancellationToken cancellationToken) =>
        Ok(await _geography.GetAdminAreasAsync(level, cancellationToken));

    [HttpGet("operational-areas")]
    public async Task<ActionResult<IReadOnlyList<OperationalAreaDto>>> OperationalAreas([FromQuery] OperationalAreaType? type, CancellationToken cancellationToken) =>
        Ok(await _geography.GetOperationalAreasAsync(type, cancellationToken));

    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<DataSourceDto>>> Sources(CancellationToken cancellationToken) =>
        Ok(await _geography.GetDataSourcesAsync(cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search([FromQuery] string q, CancellationToken cancellationToken) =>
        Ok(await _geography.SearchAsync(q, cancellationToken));
}

[ApiController]
[Route("api/layers")]
public class LayersApiController : ControllerBase
{
    private readonly IGeographyService _geography;
    private readonly IConfiguration _configuration;

    public LayersApiController(IGeographyService geography, IConfiguration configuration)
    {
        _geography = geography;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LayerDto>>> GetLayers(CancellationToken cancellationToken) =>
        Ok(await _geography.GetLayersAsync(cancellationToken));

    [HttpGet("{key}/geojson")]
    public async Task<IActionResult> GetGeoJson(string key, [FromQuery] LayerFeatureQuery query, CancellationToken cancellationToken)
    {
        query.Limit = Math.Clamp(query.Limit, 1, Math.Max(1, _configuration.GetValue("WaterWatch:MaxFeaturesPerRequest", 5000)));
        var json = await _geography.GetLayerGeoJsonAsync(key, query, cancellationToken);
        return Content(json, "application/json");
    }
}

[ApiController]
[Route("api/geocode")]
public class GeocodeApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public GeocodeApiController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(Array.Empty<object>());
        }
        q = q.Trim();
        var key = $"geocode:{q.ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out object? cached))
        {
            return Ok(cached);
        }
        try
        {
            var client = _httpClientFactory.CreateClient("nominatim");
            var url = $"/search?q={Uri.EscapeDataString(q)}&format=jsonv2&limit=5&countrycodes=ng&viewbox=6.80,4.62,7.25,5.0&bounded=1";
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var results = document.RootElement.EnumerateArray().Select(item => new
            {
                label = item.TryGetProperty("display_name", out var display) ? display.GetString() : q,
                latitude = double.TryParse(item.TryGetProperty("lat", out var lat) ? lat.GetString() : null,
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitude) ? latitude : (double?)null,
                longitude = double.TryParse(item.TryGetProperty("lon", out var lon) ? lon.GetString() : null,
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitude) ? longitude : (double?)null,
                type = item.TryGetProperty("type", out var type) ? type.GetString() : null,
                provider = "OpenStreetMap Nominatim"
            }).Where(r => r.latitude is not null && r.longitude is not null).ToList();
            _cache.Set(key, results, TimeSpan.FromMinutes(10));
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Geocoding provider unavailable", message = ex.Message });
        }
    }
}
