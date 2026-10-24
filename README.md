# WaterWatch — Port Harcourt Water Intelligence

WaterWatch is an ASP.NET Core MVC, PostgreSQL/PostGIS and Leaflet application for exploring water
infrastructure, consumption, quality, hydrology, flood exposure and field operations in Port
Harcourt City LGA and Obio/Akpor LGA, Rivers State, Nigeria.

It is an undergraduate final-year project and demonstration system. It is not an operational
utility control system and must not be used to issue public health or flood warnings.

## Project overview

The application combines an authenticated operational shell, a spatial data catalogue, a GIS
explorer, role-protected APIs and analytical workspaces. Every imported or generated record carries
a source and data classification so that observed, imported, estimated, modelled and synthetic
values can be distinguished.

The system currently implements 16 major areas:

| Area | What it does |
|---|---|
| Overview dashboard | KPIs, operational map, consumption trend, quality states, availability and priority work |
| GIS Explorer | Leaflet map, layer catalogue, filters, asset inspection, coordinate search and drawing tools |
| Infrastructure | Asset registry, inspections, maintenance records, network nodes/edges and network tracing |
| Consumption | Consumer accounts, meters, readings, CSV import, trends, anomalies and spatial aggregates |
| Water Quality | Stations, campaigns, samples, results, standards, compliance classification and trends |
| Hydrology | Stations, water-level/flow observations, Open-Meteo rainfall sync and drainage obstructions |
| Flood Intelligence | Flood layers/events, exposure analysis and clearly classified evidence states |
| Groundwater | Borehole registry, water levels, pumping tests, trends and contamination proximity |
| Leakage & NRW | DMA water balances, loss metrics, leak incidents, hotspots and pressure anomalies |
| Remote Sensing | Sentinel-2/Landsat STAC search, cached scene metadata and change-layer catalogue |
| IoT Monitoring | Device registry, API-key ingestion, telemetry readings, alert rules, alerts and SignalR |
| Spatial Analytics | Point-in-polygon, buffers, nearest assets, coverage, density, network isolation and tracing |
| Forecasting | Demand, water-level, asset-risk, leakage and water-quality baseline forecasts with metadata |
| Field Operations | Work orders and enforced status transitions from reported through verified/closed |
| Reports | CSV, XLSX and PDF exports with filters and data-classification metadata |
| Administration | Users, roles, audit trail and import history |

The older Cape Town consumption API remains available for compatibility, but the primary
application is the Port Harcourt water-intelligence workspace.

## Problem statement

Water teams often hold infrastructure, meter, water-quality and environmental data in separate
spreadsheets and GIS files. This makes it difficult to answer practical questions such as which
areas consume the most water, where leaks or quality failures cluster, which assets are exposed to
flooding and which field work should be prioritised.

WaterWatch demonstrates a single spatial platform that brings those records together with explicit
data provenance, role-based access and map-linked analytics.

## Technology stack

| Layer | Technology |
|---|---|
| Runtime | ASP.NET Core 8 MVC |
| Persistence | PostgreSQL + PostGIS, EF Core 8, Npgsql, NetTopologySuite |
| Identity | ASP.NET Core Identity with role policies and cookie authentication |
| Mapping | Leaflet, Leaflet.draw, OpenStreetMap/Esri/OpenTopoMap tile layers |
| Charts and UI | Chart.js, Bootstrap, bundled AdminLTE-style assets |
| Realtime | SignalR telemetry hub |
| Reporting | QuestPDF, ClosedXML, CsvHelper |
| Imports | GeoJSON, CSV, KML, zipped Shapefile, GeoPackage; WMS/WFS and GeoTIFF metadata registration |
| External services | Open-Meteo, Nominatim, Element 84 Earth Search STAC |
| Tests | NUnit, FluentAssertions, EF Core InMemory and optional PostGIS integration test |

## Architecture

```text
Browser: Razor views + Leaflet + Chart.js + SignalR client
  -> ASP.NET Core MVC page controllers and JSON API controllers
      -> Domain services:
           consumption, quality, hydrology, flood, groundwater,
           NRW, telemetry, forecasting, spatial analysis, imports, reports
          -> EF Core 8 + Npgsql + NetTopologySuite
              -> PostgreSQL 18 + PostGIS 3.6 (waterwatch)
  -> Optional external providers: Open-Meteo, Nominatim, Earth Search STAC
```

- Geometry is stored in EPSG:4326 with GiST indexes.
- Metric operations use PostGIS geography/UTM zone 32N calculations rather than raw degrees.
- The application is a modular monolith; no microservices are required.
- Migrations and seeding run at startup.
- Health endpoint: `/health`. Swagger: `/swagger` in Development.

## Data, source and evidence model

Bundled source extracts are under `data/sources/`:

- OCHA COD-AB administrative boundaries for Nigeria, Rivers State, Port Harcourt City LGA and
  Obio/Akpor LGA.
- OpenStreetMap settlements, waterways, water bodies and water infrastructure extracts for the Port
  Harcourt area.
- OCHA COD-PS population projection for Rivers State.

External data sources used by the application include Open-Meteo, Nominatim and Element 84 Earth
Search. Optional providers fail with an explicit availability state; the application does not
replace missing observations with invented values.

Data classifications include `Observed`, `Imported`, `Estimated`, `Modelled` and
`SyntheticDemonstration`. Utility-specific service zones, DMAs, consumer accounts, samples and
telemetry are either operator-supplied or generated only when demo mode is enabled and are visibly
labelled as synthetic.

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14 or newer with PostGIS
- Internet access for map tiles and optional external providers
- No Node.js build step: frontend assets are bundled

## Database setup

Run the database setup as a PostgreSQL superuser:

```powershell
psql -U postgres -f database/setup.sql
```

This creates:

- Database: `waterwatch`
- Application role: `waterwatch_app`
- Local development password: `changeme-local`
- Extensions: `postgis` and `postgis_raster`

The password is a local demonstration credential. Change it for any shared or deployed environment.

## Configuration

`WatchWaterConsumption/appsettings.json` contains a placeholder connection string. For local demo
mode, copy `.env.example` to `.env` and edit it:

```powershell
Copy-Item .env.example .env
```

The launcher script loads `.env` automatically. The expected values are:

```text
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=waterwatch;Username=waterwatch_app;Password=changeme-local
WaterWatch__DemoMode=true
```

Without demo mode, no synthetic operational records are created. A bootstrap administrator can be
configured with `WaterWatch__AdminEmail` and `WaterWatch__AdminPassword`.

## Run

### One-click Windows launcher

Double-click **`run.cmd`**, or right-click **`run.ps1` → Run with PowerShell**.

The launcher:

1. checks for the .NET SDK;
2. loads `.env` when present;
3. starts the MVC application and bundled frontend;
4. opens `http://localhost:5099`.

The first request can take a few seconds while migrations, boundary imports and demo data are
prepared.

### Manual run

```powershell
dotnet run --project WatchWaterConsumption\WatchWaterConsumption.csproj
```

Default URLs:

- Application: <http://localhost:5099>
- HTTPS profile: <https://localhost:7014>
- Swagger: <http://localhost:5099/swagger>
- Health: <http://localhost:5099/health>

## Demonstration accounts

Demo accounts exist only when `WaterWatch__DemoMode=true`. The password for every account is:

```text
WaterWatch#2026
```

| Email | Role |
|---|---|
| `admin@waterwatch.local` | Administrator |
| `analyst@waterwatch.local` | GIS Analyst |
| `engineer@waterwatch.local` | Water Utility Engineer |
| `hydrologist@waterwatch.local` | Hydrologist |
| `environment@waterwatch.local` | Environmental Officer |
| `inspector@waterwatch.local` | Field Inspector |
| `viewer@waterwatch.local` | Management Viewer |
| `public@waterwatch.local` | Public Viewer |

Anonymous users can view selected read-only maps and dashboards. All mutations require an
appropriate authenticated role.

## Main API areas

| Area | Representative endpoints |
|---|---|
| Geography and layers | `/api/geography/*` |
| Assets and network | `/api/assets/*`, `/api/analytics/network/*` |
| Consumption | `/api/consumption/*` |
| Water quality | `/api/water-quality/*` |
| Hydrology | `/api/hydrology/*` |
| Flood | `/api/flood/*` |
| Groundwater | `/api/groundwater/*` |
| NRW and leaks | `/api/nrw/*` |
| Telemetry | `/api/telemetry/*`, `/hubs/telemetry` |
| Forecasting | `/api/forecasting/*` |
| Reports | `/api/reports/*` |
| Imports | `/api/import/*` |
| Administration | `/api/admin/*` |

Use `/swagger` for the full request and response catalogue.

## Security

- ASP.NET Core Identity with eight application roles.
- Policy-based authorization for asset editing, imports, quality, hydrology, field operations, NRW,
  telemetry and administration.
- Antiforgery validation for browser mutations.
- Rate limiting and API exception middleware.
- Audit entries for writes and imports.
- Sensitive asset coordinates excluded from public layers and detail endpoints.
- HSTS and additional security headers in production.
- External provider credentials and database secrets are supplied through environment variables.

## Testing

Run the NUnit suite:

```powershell
dotnet test tests\WatchWaterConsumption.Tests\WatchWaterConsumption.Tests.csproj
```

The suite covers domain calculations, water-balance and quality rules, consumption import behavior
and the legacy repository. The PostGIS integration test is skipped unless
`WATERWATCH_TEST_CONNECTION` points to a disposable PostgreSQL database with PostGIS.

## Known limitations

- Demonstration records are synthetic and visibly labelled; they are not official utility data.
- Utility DMA/service-zone boundaries and the asset register are analytical demonstration sectors,
  not surveyed or official utility boundaries.
- OSM coverage is incomplete and volunteered; mapped centrelines are not engineering surveys.
- Official NIHSA flood warnings and an authoritative utility feed are not connected.
- Remote sensing requires network access to the Earth Search STAC catalogue; cached metadata is
  labelled separately.
- GeoTIFF import is metadata registration only; raster tile processing is not enabled.
- The legacy Cape Town `/api/WaterConsumption/*` endpoints remain for compatibility only.
- There is no production deployment pipeline, hardware provisioning or real device certificate
  management in this academic project.
