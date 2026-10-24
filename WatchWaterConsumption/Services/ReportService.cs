using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Domain.Enums;
using WaterWatch.Dtos;

namespace WaterWatch.Services;

public interface IReportService
{
    IReadOnlyList<ReportTypeDto> GetReportTypes();
    Task<ReportFileDto> GenerateAsync(ReportRequest request, string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportJobDto>> GetJobsAsync(CancellationToken cancellationToken = default);
}

public class ReportService : IReportService
{
    private readonly DatabaseContext _db;
    private readonly IAssetService _assets;
    private readonly IConsumptionService _consumption;
    private readonly IQualityService _quality;
    private readonly IWorkOrderService _workOrders;
    private readonly INrwService _nrw;
    private readonly IFloodService _flood;
    private readonly ILogger<ReportService> _logger;

    public ReportService(DatabaseContext db, IAssetService assets, IConsumptionService consumption,
        IQualityService quality, IWorkOrderService workOrders, INrwService nrw, IFloodService flood,
        ILogger<ReportService> logger)
    {
        _db = db;
        _assets = assets;
        _consumption = consumption;
        _quality = quality;
        _workOrders = workOrders;
        _nrw = nrw;
        _flood = flood;
        _logger = logger;
    }

    public IReadOnlyList<ReportTypeDto> GetReportTypes() => new[]
    {
        new ReportTypeDto("assets", "Infrastructure asset register", "Registered water assets with type, status, condition, location and source.", new[] { "Csv", "Xlsx", "Pdf" }),
        new ReportTypeDto("consumption", "Consumption and demand", "Measured consumption aggregated by administrative area, zone or DMA.", new[] { "Csv", "Xlsx", "Pdf" }),
        new ReportTypeDto("water-quality", "Water quality results", "Sample results with parameter, value, unit, classification and standard.", new[] { "Csv", "Xlsx", "Pdf" }),
        new ReportTypeDto("work-orders", "Field operations", "Work orders with status, priority, assignee and dates.", new[] { "Csv", "Xlsx", "Pdf" }),
        new ReportTypeDto("nrw", "Non-revenue water", "DMA water balances with NRW and evidenced loss components.", new[] { "Csv", "Xlsx", "Pdf" }),
        new ReportTypeDto("flood-exposure", "Flood exposure", "Persisted exposure analyses with counts, method and limitations.", new[] { "Csv", "Xlsx", "Pdf" }),
        new ReportTypeDto("data-coverage", "Data quality and coverage", "Record counts, temporal coverage and availability states by module.", new[] { "Csv", "Xlsx", "Pdf" })
    };

    public async Task<ReportFileDto> GenerateAsync(ReportRequest request, string userName, CancellationToken cancellationToken = default)
    {
        var rows = new List<Dictionary<string, string?>>();
        var columns = new List<string>();
        string title;
        var format = request.Format;
        try
        {
            switch (request.ReportType.ToLowerInvariant())
            {
                case "assets":
                {
                    title = "Water infrastructure asset register";
                    var assets = await _assets.GetAssetsAsync(new AssetListQuery
                    {
                        PageSize = 500, Search = request.Search, AdminAreaId = request.AdminAreaId,
                        OperationalAreaId = request.OperationalAreaId
                    }, cancellationToken);
                    columns = new List<string> { "Asset code", "Name", "Type", "Status", "Condition", "Admin area", "Service zone", "Longitude", "Latitude", "Classification" };
                    rows = assets.Items.Select(a => Row(
                        ("Asset code", a.AssetCode), ("Name", a.Name), ("Type", a.AssetType.ToString()),
                        ("Status", a.Status.ToString()), ("Condition", a.Condition.ToString()),
                        ("Admin area", a.AdminAreaName), ("Service zone", a.OperationalAreaName),
                        ("Longitude", a.Longitude?.ToString("0.000000", CultureInfo.InvariantCulture)),
                        ("Latitude", a.Latitude?.ToString("0.000000", CultureInfo.InvariantCulture)),
                        ("Classification", a.DataClassification.ToString()))).ToList();
                    break;
                }
                case "consumption":
                {
                    title = "Consumption and demand report";
                    var dimension = string.IsNullOrWhiteSpace(request.Dimension) ? "adminarea" : request.Dimension!;
                    var aggregates = await _consumption.GetAggregatesAsync(dimension, request.From, request.To, cancellationToken);
                    columns = new List<string> { "Area", "Consumption m3", "Consumption litres", "Accounts", "Population", "Litres per capita per day", "Classification" };
                    rows = aggregates.Select(a => Row(
                        ("Area", a.Label), ("Consumption m3", a.ConsumptionM3.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("Consumption litres", a.ConsumptionLitres.ToString("0", CultureInfo.InvariantCulture)),
                        ("Accounts", a.Accounts.ToString(CultureInfo.InvariantCulture)),
                        ("Population", a.Population?.ToString(CultureInfo.InvariantCulture)),
                        ("Litres per capita per day", a.LitresPerCapitaPerDay?.ToString("0.0", CultureInfo.InvariantCulture)),
                        ("Classification", a.DataClassification.ToString()))).ToList();
                    break;
                }
                case "water-quality":
                {
                    title = "Water quality results";
                    var samples = await _quality.GetSamplesAsync(new QualityQuery
                    {
                        Page = 1, PageSize = 200,
                        From = request.From, To = request.To
                    }, cancellationToken);
                    columns = new List<string> { "Sampled at", "Sample", "Station", "Parameter", "Value", "Unit", "State", "Standard", "Assessment", "Classification" };
                    rows = samples.Items.SelectMany(s => s.Results.Select(r => Row(
                        ("Sampled at", s.SampledAt.ToString("yyyy-MM-dd HH:mm")),
                        ("Sample", s.SampleCode), ("Station", s.StationName),
                        ("Parameter", r.Parameter.ToString()),
                        ("Value", r.Value?.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("Unit", r.Unit), ("State", r.State.ToString()), ("Standard", r.StandardCode),
                        ("Assessment", r.AssessmentNote), ("Classification", s.DataClassification.ToString())))).ToList();
                    break;
                }
                case "work-orders":
                {
                    title = "Field operations work orders";
                    var orders = await _workOrders.GetWorkOrdersAsync(new WorkOrderQuery { Page = 1, PageSize = 500 }, cancellationToken);
                    columns = new List<string> { "Reference", "Title", "Type", "Priority", "Status", "Asset", "Assigned to", "Due", "Created", "Closed" };
                    rows = orders.Items.Select(w => Row(
                        ("Reference", w.Reference), ("Title", w.Title), ("Type", w.Type.ToString()),
                        ("Priority", w.Priority.ToString()), ("Status", w.Status.ToString()),
                        ("Asset", w.AssetName), ("Assigned to", w.AssignedTo),
                        ("Due", w.DueAt?.ToString("yyyy-MM-dd")), ("Created", w.CreatedAt.ToString("yyyy-MM-dd")),
                        ("Closed", w.ClosedAt?.ToString("yyyy-MM-dd")))).ToList();
                    break;
                }
                case "nrw":
                {
                    title = "Non-revenue water (DMA water balance)";
                    var balances = await _nrw.GetBalancesAsync(null, request.From, request.To, cancellationToken);
                    columns = new List<string> { "DMA", "Period start", "Period end", "Input m3", "Billed m3", "NRW m3", "NRW %", "Unallocated m3", "Loss split evidence", "Classification" };
                    rows = balances.Select(b => Row(
                        ("DMA", b.DmaName), ("Period start", b.PeriodStart.ToString("yyyy-MM-dd")),
                        ("Period end", b.PeriodEnd.ToString("yyyy-MM-dd")),
                        ("Input m3", b.InputVolumeM3.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("Billed m3", b.BilledVolumeM3.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("NRW m3", b.NonRevenueWaterM3.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("NRW %", b.NonRevenueWaterPercent.ToString("0.0", CultureInfo.InvariantCulture)),
                        ("Unallocated m3", b.UnallocatedLossM3.ToString("0.###", CultureInfo.InvariantCulture)),
                        ("Loss split evidence", b.LossSplitEvidence), ("Classification", b.DataClassification.ToString()))).ToList();
                    break;
                }
                case "flood-exposure":
                {
                    title = "Flood exposure analyses";
                    var analyses = await _flood.GetExposureResultsAsync(cancellationToken);
                    columns = new List<string> { "Flood layer", "Analysed at", "Analysed by", "Assets exposed", "Stations exposed", "Devices exposed", "Population exposed", "Classification", "Limitations" };
                    rows = analyses.Select(a => Row(
                        ("Flood layer", a.FloodLayerName), ("Analysed at", a.AnalysedAt.ToString("yyyy-MM-dd HH:mm")),
                        ("Analysed by", a.AnalysedBy), ("Assets exposed", a.AssetsExposed.ToString(CultureInfo.InvariantCulture)),
                        ("Stations exposed", a.SamplingStationsExposed.ToString(CultureInfo.InvariantCulture)),
                        ("Devices exposed", a.DevicesExposed.ToString(CultureInfo.InvariantCulture)),
                        ("Population exposed", a.PopulationExposed?.ToString(CultureInfo.InvariantCulture)),
                        ("Classification", a.DataClassification.ToString()), ("Limitations", a.Limitations))).ToList();
                    break;
                }
                case "data-coverage":
                {
                    title = "Data quality and coverage";
                    columns = new List<string> { "Dataset", "Records", "Earliest", "Latest", "Availability", "Classification" };
                    rows = await BuildCoverageRowsAsync(cancellationToken);
                    break;
                }
                default:
                    throw new ArgumentException($"Unknown report type '{request.ReportType}'.");
            }

            var metadata = new[]
            {
                $"Report: {title}",
                $"Generated: {DateTimeOffset.UtcNow:u}",
                $"Requested by: {userName}",
                $"Period: {(request.From?.ToString("yyyy-MM-dd") ?? "all")} to {(request.To?.ToString("yyyy-MM-dd") ?? "all")}",
                $"Search/filter: {request.Search ?? "none"}",
                "WaterWatch — Port Harcourt water intelligence platform",
                "Records may include synthetic demonstration, estimated, modelled or imported data; classification is shown per row where available."
            };

            var content = format switch
            {
                ReportFormat.Csv => BuildCsv(title, metadata, columns, rows),
                ReportFormat.Xlsx => BuildExcel(title, metadata, columns, rows),
                ReportFormat.Pdf => BuildPdf(title, metadata, columns, rows),
                _ => throw new ArgumentException("Unsupported report format.")
            };
            var contentType = format switch
            {
                ReportFormat.Csv => "text/csv",
                ReportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/pdf"
            };
            var extension = format.ToString().ToLowerInvariant();
            var job = new ReportJob
            {
                Name = title, ReportType = request.ReportType, Format = format, RequestedBy = userName,
                ParametersJson = System.Text.Json.JsonSerializer.Serialize(request),
                Status = "Completed", CompletedAt = DateTimeOffset.UtcNow, RecordCount = rows.Count,
                DataClassification = rows.Any(IsSyntheticRow) ? DataClassification.SyntheticDemonstration : DataClassification.Observed
            };
            _db.ReportJobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);
            return new ReportFileDto(content, contentType,
                $"waterwatch-{request.ReportType}-{DateTime.UtcNow:yyyyMMdd-HHmm}.{extension}", rows.Count, request.ReportType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Report generation failed for {ReportType}", request.ReportType);
            var job = new ReportJob
            {
                Name = request.ReportType, ReportType = request.ReportType, Format = request.Format,
                RequestedBy = userName, ParametersJson = System.Text.Json.JsonSerializer.Serialize(request),
                Status = "Failed", Error = ex.Message, CompletedAt = DateTimeOffset.UtcNow
            };
            _db.ReportJobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ReportJobDto>> GetJobsAsync(CancellationToken cancellationToken = default) =>
        await _db.ReportJobs.AsNoTracking().OrderByDescending(j => j.CreatedAt).Take(50)
            .Select(j => new ReportJobDto(j.Id, j.Name, j.ReportType, j.Format, j.Status, j.RecordCount,
                j.CreatedAt, j.CompletedAt, j.Error))
            .ToListAsync(cancellationToken);

    private async Task<List<Dictionary<string, string?>>> BuildCoverageRowsAsync(CancellationToken cancellationToken)
    {
        async Task<Dictionary<string, string?>> RowFor(string name, int count, DateTimeOffset? first, DateTimeOffset? last, string availability, DataClassification classification) =>
            Row(("Dataset", name), ("Records", count.ToString(CultureInfo.InvariantCulture)),
                ("Earliest", first?.ToString("yyyy-MM-dd")), ("Latest", last?.ToString("yyyy-MM-dd")),
                ("Availability", availability), ("Classification", classification.ToString()));

        var rows = new List<Dictionary<string, string?>>
        {
            await RowFor("Administrative areas", await _db.AdminAreas.CountAsync(cancellationToken),
                await _db.AdminAreas.MinAsync(a => (DateTimeOffset?)a.CreatedAt, cancellationToken),
                await _db.AdminAreas.MaxAsync(a => (DateTimeOffset?)a.CreatedAt, cancellationToken),
                "Loaded boundaries; see data inventory", DataClassification.Imported),
            await RowFor("Water assets", await _db.Assets.CountAsync(cancellationToken),
                await _db.Assets.MinAsync(a => (DateTimeOffset?)a.CreatedAt, cancellationToken),
                await _db.Assets.MaxAsync(a => (DateTimeOffset?)a.CreatedAt, cancellationToken),
                "See asset source/classification", DataClassification.Observed),
            await RowFor("Meter readings", await _db.MeterReadings.CountAsync(cancellationToken),
                null, null, "See consumption page for coverage", DataClassification.Observed),
            await RowFor("Water quality samples", await _db.WaterSamples.CountAsync(cancellationToken),
                await _db.WaterSamples.MinAsync(s => (DateTimeOffset?)s.SampledAt, cancellationToken),
                await _db.WaterSamples.MaxAsync(s => (DateTimeOffset?)s.SampledAt, cancellationToken),
                "Results classified by standard", DataClassification.Observed),
            await RowFor("Hydrology observations", await _db.HydrologyObservations.CountAsync(cancellationToken),
                await _db.HydrologyObservations.MinAsync(o => (DateTimeOffset?)o.ObservedAt, cancellationToken),
                await _db.HydrologyObservations.MaxAsync(o => (DateTimeOffset?)o.ObservedAt, cancellationToken),
                "Includes Open-Meteo reanalysis where labelled", DataClassification.Imported),
            await RowFor("Telemetry readings", await _db.TelemetryReadings.CountAsync(cancellationToken),
                await _db.TelemetryReadings.MinAsync(t => (DateTimeOffset?)t.ObservedAt, cancellationToken),
                await _db.TelemetryReadings.MaxAsync(t => (DateTimeOffset?)t.ObservedAt, cancellationToken),
                "Synthetic demo readings are labelled", DataClassification.Observed),
            await RowFor("Groundwater readings", await _db.GroundwaterReadings.CountAsync(cancellationToken),
                await _db.GroundwaterReadings.MinAsync(g => (DateTimeOffset?)g.ObservedAt, cancellationToken),
                await _db.GroundwaterReadings.MaxAsync(g => (DateTimeOffset?)g.ObservedAt, cancellationToken),
                "Borehole readings", DataClassification.Observed),
            await RowFor("Flood layers", await _db.FloodLayers.CountAsync(cancellationToken),
                await _db.FloodLayers.MinAsync(f => (DateTimeOffset?)f.CreatedAt, cancellationToken),
                await _db.FloodLayers.MaxAsync(f => (DateTimeOffset?)f.CreatedAt, cancellationToken),
                "Check layer type/official status; not all layers are warnings", DataClassification.Modelled),
            await RowFor("Work orders", await _db.WorkOrders.CountAsync(cancellationToken),
                await _db.WorkOrders.MinAsync(w => (DateTimeOffset?)w.CreatedAt, cancellationToken),
                await _db.WorkOrders.MaxAsync(w => (DateTimeOffset?)w.CreatedAt, cancellationToken),
                "Operational workflow", DataClassification.Observed)
        };
        return rows;
    }

    private static byte[] BuildCsv(string title, string[] metadata, List<string> columns, List<Dictionary<string, string?>> rows)
    {
        var builder = new StringBuilder();
        foreach (var line in metadata)
        {
            builder.AppendLine("# " + line);
        }
        builder.AppendLine();
        builder.AppendLine(string.Join(",", columns.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", columns.Select(c => Escape(row.GetValueOrDefault(c)))));
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] BuildExcel(string title, string[] metadata, List<string> columns, List<Dictionary<string, string?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        var rowIndex = 1;
        foreach (var line in metadata)
        {
            sheet.Cell(rowIndex++, 1).Value = line;
        }
        rowIndex++;
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(rowIndex, c + 1);
            cell.Value = columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        }
        foreach (var row in rows)
        {
            rowIndex++;
            for (var c = 0; c < columns.Count; c++)
            {
                sheet.Cell(rowIndex, c + 1).Value = row.GetValueOrDefault(columns[c]) ?? string.Empty;
            }
        }
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdf(string title, string[] metadata, List<string> columns, List<Dictionary<string, string?>> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var visibleColumns = columns.Take(8).ToList();
        var visibleRows = rows.Take(300).ToList();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(7).FontFamily("Arial"));
                page.Header().Column(header =>
                {
                    header.Item().Text(title).FontSize(15).Bold().FontColor(Colors.Blue.Darken3);
                    header.Item().Text(metadata[0]).FontSize(7).FontColor(Colors.Grey.Darken2);
                    header.Item().Text(metadata[1]).FontSize(7).FontColor(Colors.Grey.Darken2);
                    header.Item().Text(metadata[2]).FontSize(7).FontColor(Colors.Grey.Darken2);
                    header.Item().Text(metadata[5]).FontSize(7).FontColor(Colors.Grey.Darken2);
                });
                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(definition =>
                    {
                        foreach (var _ in visibleColumns)
                        {
                            definition.RelativeColumn();
                        }
                    });
                    foreach (var column in visibleColumns)
                    {
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(2).Text(column).Bold();
                    }
                    foreach (var row in visibleRows)
                    {
                        foreach (var column in visibleColumns)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2)
                                .Text(row.GetValueOrDefault(column) ?? string.Empty);
                        }
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("WaterWatch · page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    if (rows.Count > visibleRows.Count)
                    {
                        text.Span($" · truncated to {visibleRows.Count} of {rows.Count} rows; use CSV/XLSX for the full dataset.");
                    }
                });
            });
        }).GeneratePdf();
    }

    private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static bool IsSyntheticRow(Dictionary<string, string?> row) =>
        row.Values.Any(v => v != null && v.Contains("SyntheticDemonstration", StringComparison.OrdinalIgnoreCase));
}
