using Microsoft.EntityFrameworkCore;
using WatchWaterConsumption;
using WatchWaterConsumption.Extensions;
using WatchWaterConsumption.Repositories;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// var connectionString = Environment.GetEnvironmentVariable(ApplicationConstants.CONNECTION_STRING);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string environment variable is not set.");
}

builder.Services.AddDbContext<DatabaseContext>(options => options.UseNpgsql(connectionString), ServiceLifetime.Transient);
builder.Services.AddScoped<IWaterConsumptionRepository, WaterConsumptionRepository>();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add MiniProfiler
builder.Services.AddMiniProfiler(opt =>
{
    opt.RouteBasePath = "/profiler";
    opt.PopupRenderPosition = StackExchange.Profiling.RenderPosition.Left;
}).AddEntityFramework();
builder.Services.AddCors();
// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Watch Water Consumption API",
        Version = "v1"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMiniProfiler();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseCors(x =>
{
    x.AllowAnyOrigin();
    x.AllowAnyHeader();
    x.AllowAnyMethod();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
// specifying the Swagger JSON endpoint.
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Watch Water Consumption API v1");
    c.RoutePrefix = "swagger"; // Optional: set Swagger UI at "/swagger"
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Migrate();
app.Run();
