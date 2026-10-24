using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using StackExchange.Profiling;
using WatchWaterConsumption.Extensions;
using WatchWaterConsumption.Repositories;
using WaterWatch.Data;
using WaterWatch.Domain.Entities;
using WaterWatch.Middleware;
using WaterWatch.Realtime;
using WaterWatch.Security;
using WaterWatch.Services;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();
        }));
builder.Services.AddScoped<IWaterConsumptionRepository, WaterConsumptionRepository>();
builder.Services.AddMiniProfiler(options =>
{
    options.RouteBasePath = "/profiler";
    options.PopupRenderPosition = RenderPosition.Left;
}).AddEntityFramework();
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<DatabaseContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "WaterWatch.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    foreach (var (name, roles) in Policies.Definitions)
    {
        options.AddPolicy(name, policy => policy.RequireRole(roles));
    }
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "WaterWatch.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
}).AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WaterWatch Port Harcourt API",
        Version = "v1",
        Description = "Water resources, infrastructure, consumption, quality, hydrology, flood, telemetry and operations API for Port Harcourt."
    });
});

builder.Services.AddScoped<IGeoJsonService, GeoJsonService>();
builder.Services.AddScoped<IGeographyService, GeographyService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IConsumptionService, ConsumptionService>();
builder.Services.AddScoped<IQualityService, QualityService>();
builder.Services.AddScoped<IHydrologyService, HydrologyService>();
builder.Services.AddScoped<IFloodService, FloodService>();
builder.Services.AddScoped<IGroundwaterService, GroundwaterService>();
builder.Services.AddScoped<INrwService, NrwService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddScoped<ISpatialAnalysisService, SpatialAnalysisService>();
builder.Services.AddScoped<IForecastService, ForecastService>();
builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IRemoteSensingService, RemoteSensingService>();
builder.Services.AddScoped<IWeatherService, OpenMeteoWeatherService>();
builder.Services.AddHttpClient("nominatim", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WaterWatch:ExternalData:NominatimBaseUrl"] ?? "https://nominatim.openstreetmap.org");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.Configuration["WaterWatch:ExternalData:NominatimUserAgent"] ?? "WaterWatch/1.0 (contact: data@waterwatch.local)");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient("open-meteo", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WaterWatch:ExternalData:OpenMeteoBaseUrl"] ?? "https://api.open-meteo.com");
    client.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddHttpClient("open-meteo-archive", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WaterWatch:ExternalData:OpenMeteoArchiveBaseUrl"] ?? "https://archive-api.open-meteo.com");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient("stac", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WaterWatch:ExternalData:EarthSearchStacUrl"] ?? "https://earth-search.aws.element84.com/v1");
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WaterWatch/1.0 (Port Harcourt water intelligence)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json");
});
builder.Services.AddHostedService<TelemetryBackgroundService>();
builder.Services.AddHttpClient("nominatim-search", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WaterWatch:ExternalData:NominatimBaseUrl"] ?? "https://nominatim.openstreetmap.org");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.Configuration["WaterWatch:ExternalData:NominatimUserAgent"] ?? "WaterWatch/1.0 (contact: data@waterwatch.local)");
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMiniProfiler();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<ApiExceptionMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(self), camera=(self)";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WaterWatch API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapHub<TelemetryHub>("/hubs/telemetry");

app.MapGet("/health", async (DatabaseContext db, CancellationToken cancellationToken) =>
{
    var database = await db.Database.CanConnectAsync(cancellationToken);
    var postgis = false;
    if (database)
    {
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT PostGIS_Version()";
            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                await command.Connection!.OpenAsync(cancellationToken);
            }
            postgis = await command.ExecuteScalarAsync(cancellationToken) is string;
        }
        catch
        {
            postgis = false;
        }
    }

    return Results.Json(new
    {
        status = database && postgis ? "healthy" : "degraded",
        database,
        postgis,
        utc = DateTimeOffset.UtcNow
    }, statusCode: database && postgis ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.Migrate();
await SeedData.InitializeAsync(app.Services, app.Configuration);

app.Run();
