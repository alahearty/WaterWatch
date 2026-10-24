# WaterWatch

## Overview

WaterWatch is an ASP.NET Core MVC + Leaflet application for visualising household water
consumption in Cape Town suburbs. The home page draws colour-coded suburb polygons from a bundled
GeoJSON dataset and shows the top ten consumers in a bar chart. Data is imported from
`WatchWaterConsumption/map.geojson` into PostgreSQL on first access.

## Problem statement

Water utilities and municipalities need an easy way to see which neighbourhoods consume the most
water. WaterWatch turns an open GeoJSON dataset into an interactive choropleth map and ranking so
patterns can be inspected without a desktop GIS package.

## Aim

To provide an accessible GIS dashboard that imports neighbourhood water-consumption data and presents it as a map and top-ten ranking.

## Objectives

- Import neighbourhood polygons and consumption values from GeoJSON into PostgreSQL.
- Display a choropleth map coloured by average monthly consumption.
- Show suburb details in map popups.
- Show the top ten consuming neighbourhoods in a chart.
- Demonstrate EF Core migrations, a repository layer and a Leaflet front end in ASP.NET Core MVC.

## Technology stack

| Layer | Technology |
|---|---|
| Runtime | ASP.NET Core 8 MVC |
| Database | PostgreSQL + EF Core 8 (Npgsql) |
| Mapping | Leaflet 1.9, OpenStreetMap/Esri tile layers |
| Charting | Chart.js 2.4 |
| UI theme | AdminLTE 2 / Bootstrap 3 (bundled under `wwwroot/app`) |
| Tests | No automated test project is included |

## Architecture

```
Browser (Leaflet + jQuery + Chart.js)
  -> ASP.NET Core MVC HomeController (single map page)
  -> /api/WaterConsumption/* (WaterConsumptionController)
      -> WaterConsumptionRepository
          -> EF Core DatabaseContext
              -> PostgreSQL `cptwater`
  map.geojson imported on first GetAll call
```

The API controller is namespaced `WaterWatch.Controllers` but lives under
`WatchWaterConsumption/Controllers`.

## Software prerequisites

- .NET 8 SDK
- PostgreSQL 14+
- Internet access for map tiles (or modify the tile URLs)
- No Node.js build step: all front-end assets are bundled

## Database setup

Create a database and role (the default configuration uses `worinium`):

```sql
CREATE USER worinium WITH PASSWORD 'worisoft@123';
CREATE DATABASE cptwater OWNER worinium;
```

Default connection string (`appsettings.json`):

```
Host=localhost;Port=5432;Database=cptwater;Username=worinium;Password=worisoft@123
```

The migration `20240529101822_InitialMigration` creates the `Consumptions` table. The application
runs `Database.Migrate()` at startup, so no manual `dotnet ef` step is required. Data is imported
lazily the first time `GET /api/WaterConsumption/getall` is called.

## Environment configuration

Override the connection string with:

```
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=cptwater;Username=worinium;Password=worisoft@123
ASPNETCORE_ENVIRONMENT=Development
```

See `.env.example`. ASP.NET Core does not auto-load `.env` files; set variables in the shell.

## Build

```powershell
dotnet build WaterWatch.sln
```

## Run

```powershell
dotnet run --project WatchWaterConsumption\WatchWaterConsumption.csproj
```

Default development URLs from `launchSettings.json`:

- HTTP: <http://localhost:5099>
- HTTPS: <https://localhost:7014>
- Swagger: <http://localhost:5099/swagger>

Open the map at <http://localhost:5099/>. The first load can take a few seconds while 119
GeoJSON features are imported; refresh the page after the API finishes.

## Demo data

No credentials are required. The bundled `map.geojson` is the demonstration dataset (Cape Town
suburbs). The importer skips malformed features that have no neighbourhood name or consumption
value, leaving 113 valid polygons on the current dataset.

## Demonstration procedure

1. Open the dashboard and allow the first GeoJSON import to finish.
2. Show the choropleth map, legend and basemap switcher.
3. Hover a polygon to show its suburb group, neighbourhood and KL value.
4. Show the top-ten consumer chart.
5. Open `/api/WaterConsumption/getall` and `/api/WaterConsumption/topten` to show the API data.
6. Refresh the page to demonstrate persisted database reads.

The full academic presentation script is in `DEMO_GUIDE.md`.

## API endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/WaterConsumption/getall` | Import data if needed and return all valid polygons |
| GET | `/api/WaterConsumption/topten` | Return the ten highest-consumption neighbourhoods |
| GET | `/swagger` | Swagger UI |

## Main features

- Leaflet map with five switchable base layers.
- Choropleth colour scale for average monthly water use (KL).
- Hover popups with suburb group, neighbourhood and consumption.
- Horizontal bar chart of the top ten consumers.
- Automatic EF Core migration and GeoJSON seeding.

## Testing

The repository includes four NUnit tests in
`tests/WatchWaterConsumption.Tests`. They use an in-memory database and a copied
`map.geojson`, so no PostgreSQL instance is needed for the test run.

```powershell
dotnet test tests\WatchWaterConsumption.Tests\WatchWaterConsumption.Tests.csproj
```

Expected result: `Passed: 4`.

The tests cover:

- GeoJSON import and filtering of malformed/NULL rows (113 valid rows).
- Top-ten ordering and NULL exclusion.
- Filtering of NULL rows already present in the database.
- Missing GeoJSON file handling without an exception.

Manual smoke test performed during the audit:

1. Start PostgreSQL and the app.
2. Open <http://localhost:5099/>.
3. Confirm the map renders suburb polygons and the legend.
4. Confirm `GET /api/WaterConsumption/getall` returns 113 records with non-null names/values.
5. Confirm `GET /api/WaterConsumption/topten` returns 10 records.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Blank page / `L is not defined` | The third-party scripts must load before the page script. This was fixed in `_Layout.cshtml`; rebuild after editing Razor views. |
| Map empty or chart missing | Open the browser console and check `/api/WaterConsumption/getall`; the first import can take several seconds. |
| Npgsql connection error | Create the `cptwater` database and check the connection string. |
| Duplicate data after re-import | The importer only runs when `Consumptions` is empty. Delete the table rows or recreate the database to re-import. |
| Map tiles do not load | Some basemaps use HTTP/Esri endpoints; try the OpenStreetMap layer or use a network that allows those hosts. |

## Known limitations

- No authentication or authorisation; the map and API are public.
- The dataset is a single 2017 snapshot; there is no upload or editing UI.
- Some GeoJSON features are malformed and are filtered out.
- Chart.js 2.4 and AdminLTE 2 are old bundled assets retained from the original project.
- Tests cover the data/import layer only; there is no browser automation or CI pipeline in the
  project itself.
