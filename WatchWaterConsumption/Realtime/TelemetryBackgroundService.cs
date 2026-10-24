using WaterWatch.Services;

namespace WaterWatch.Realtime;

public class TelemetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelemetryBackgroundService> _logger;

    public TelemetryBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TelemetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var demoMode = _configuration.GetValue("WaterWatch:DemoMode", false);
        var interval = TimeSpan.FromSeconds(Math.Clamp(_configuration.GetValue("WaterWatch:DemoTelemetryIntervalSeconds", 60), 15, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
                if (demoMode)
                {
                    await telemetry.GenerateDemoReadingAsync(stoppingToken);
                }
                await telemetry.EscalateDueAlertsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Telemetry background cycle failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
