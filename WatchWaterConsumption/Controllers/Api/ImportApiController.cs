using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterWatch.Dtos;
using WaterWatch.Security;
using WaterWatch.Services;

namespace WaterWatch.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Policy = Policies.CanImportData)]
public class ImportApiController : ControllerBase
{
    private static readonly string[] Allowed = { ".geojson", ".json", ".csv", ".kml", ".zip", ".gpkg", ".shp" };
    private readonly IImportService _import;

    public ImportApiController(IImportService import) => _import = import;

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    public async Task<ActionResult<ImportResultDto>> Upload(IFormFile file, [FromForm] ImportOptions options, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "A file is required." });
        }
        if (file.Length > 50 * 1024 * 1024)
        {
            return BadRequest(new { error = "Files are limited to 50 MB." });
        }
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(extension))
        {
            return BadRequest(new { error = $"Unsupported file extension '{extension}'." });
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        await using var stream = file.OpenReadStream();
        var result = await _import.ImportAsync(stream, Path.GetFileName(file.FileName), options,
            User.Identity?.Name ?? "unknown", cancellationToken);
        return Ok(result);
    }

    [HttpGet("formats")]
    public ActionResult<object> Formats() => Ok(new
    {
        formats = new[]
        {
            new { extension = ".geojson / .json", support = "Native GeoJSON (RFC 7946); CRS declared as EPSG:4326 by default." },
            new { extension = ".csv", support = "lon/lat or longitude/latitude or x/y columns, or a WKT geometry column." },
            new { extension = ".kml", support = "Point, LineString and Polygon Placemarks." },
            new { extension = ".zip", support = "Zipped Shapefile containing .shp/.shx/.dbf; declared SRID required for non-4326 data." },
            new { extension = ".gpkg", support = "GeoPackage geometry tables (EPSG:4326, 3857 or 32632)." },
            new { extension = ".shp", support = "Not accepted alone; upload the zipped set with sidecars." }
        },
        geotiff = "Register GeoTIFF metadata with /api/import/raster-metadata; raster tile processing is not enabled.",
        services = "Register WMS/WFS with /api/import/service-layer."
    });

    [HttpPost("service-layer")]
    public async Task<ActionResult<LayerDto>> RegisterService([FromBody] ServiceLayerCreateRequest request, CancellationToken cancellationToken) =>
        Ok(await _import.RegisterServiceLayerAsync(request, cancellationToken));

    [HttpPost("raster-metadata")]
    public async Task<ActionResult<LayerDto>> RegisterRaster([FromBody] RasterMetadataRequest request, CancellationToken cancellationToken) =>
        Ok(await _import.RegisterRasterMetadataAsync(request, cancellationToken));
}
