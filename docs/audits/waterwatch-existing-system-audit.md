# WaterWatch — Existing System Audit

Audit date: 2026-10-23  
Auditor: implementation engineering pass  
Repository: `C:\SyncGrass Limited\Topics\WaterWatch`  
Branch: `master` (working tree contains uncommitted documentation/test additions)

## 1. Executive summary

The repository is a small ASP.NET Core 8 MVC application that renders a Cape Town neighbourhood
water-consumption choropleth. It has a single EF Core entity, two read-only API endpoints, one Razor
page and a bundled AdminLTE/Leaflet/Chart.js front end. It is a demonstration of GeoJSON import and
map rendering, not a water-utility platform.

The application builds and has four NUnit tests for the import/repository layer. PostgreSQL is
required at runtime; an in-memory provider is used by tests. There is no authentication, no write
API, no spatial database type, no spatial index, no operational data model and no GIS analysis.

This audit records the state **before** the Port Harcourt implementation. It is the baseline for
the target architecture and backlog.

## 2. Repository inventory

| Area | Evidence | Finding |
|---|---|---|
| Solution | `WaterWatch.sln` | One web project plus `tests/WatchWaterConsumption.Tests` (untracked addition). |
| Runtime | `WatchWaterConsumption/WatchWaterConsumption.csproj` | .NET 8, nullable enabled, implicit usings. |
| Web framework | `Program.cs` | ASP.NET Core MVC with controllers and Razor views. |
| Database | `DatabaseContext/DatabaseContext.cs` | PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.4; one `Consumptions` table. |
| ORM/migrations | `Migrations/20240529101822_InitialMigration.cs` | EF Core 8, auto-migrate at startup via `MigrationExtension`. |
| Mapping | `Views/Home/Index.cshtml`, `wwwroot/app/leaflet` | Leaflet 1.9 bundled locally. |
| Charting | `Views/Home/Index.cshtml`, `wwwroot/app/extscripts` | Chart.js 2.4 (horizontal bar chart). |
| UI framework | `Views/Shared/_Layout.cshtml`, `wwwroot/app/admin` | AdminLTE 2 / Bootstrap 3 / Font Awesome 4. |
| Auth | none | No Identity, authentication middleware, authorization attributes or user model. |
| Realtime | none | No SignalR, WebSocket or telemetry subsystem. |
| Tests | `tests/WatchWaterConsumption.Tests` | 4 NUnit tests; EF Core InMemory; no API/UI/integration tests. |
| Deployment | none | No Dockerfile, CI workflow or environment-specific deployment configuration. |
| Documentation | `README.md`, `DEMO_GUIDE.md` | Accurate for the Cape Town demo; no platform documentation. |

## 3. Current request/data flow

```
Browser (Leaflet + jQuery + Chart.js)
  -> Home/Index (map page)
  -> GET /api/WaterConsumption/getall
       -> WaterConsumptionRepository.GetAll()
            -> SaveDataAsync() reads WatchWaterConsumption/map.geojson if table empty
            -> SELECT Consumptions WHERE name/value not null
  -> GET /api/WaterConsumption/topten
       -> SELECT top 10 by AverageMonthlyKL
  -> PostgreSQL cptwater / Consumptions
```

The API controller is declared in namespace `WaterWatch.Controllers` but located under
`WatchWaterConsumption/Controllers/WaterConsumptionController.cs`. The repository catches all
import exceptions and writes to the console, so a failed import is reported to the browser as an
empty dataset rather than an error.

## 4. Existing functionality assessment

| Capability | State | Notes |
|---|---|---|
| Map opens | WORKS | Initial view `[-33.925, 18.625]`, zoom 11 (Cape Town), contradicting the product objective. |
| Basemap switcher | WORKS | Five tile layers; several URLs are HTTP and two no longer reliably serve tiles. |
| Choropleth | WORKS | Hardcoded seven-class colour scale and fixed KL thresholds. |
| Popups | WORKS | Hover-only; opens/closes on mouseover/out, which is poor for touch and keyboard users. |
| Top-ten chart | WORKS | Chart.js 2 "horizontalBar" type; old but functional. |
| Legend | WORKS | Generated from the same hardcoded thresholds. |
| Data import | WORKS (demo) | Lazy, non-transactional, no validation report, no source metadata, no dedupe. |
| Database persistence | WORKS | Migrations at startup; single table. |
| API documentation | WORKS | Swagger UI at `/swagger`. |
| Authentication/authorization | MISSING | All endpoints are public. |
| Spatial database | MISSING | Coordinates stored as text JSON, no PostGIS, no SRID, no GiST index. |
| CRUD | MISSING | Read-only API; no create/update/delete. |
| Real-time / telemetry | MISSING | No device or time-series model. |
| Reporting | MISSING | No exports or reports. |
| Tests | PARTIAL | Repository tests only. |

## 5. Data assessment

The displayed South African data is **hardcoded via a bundled file**
(`WatchWaterConsumption/map.geojson`), not fetched from any external API. It is imported once into
the `Consumptions` table on first `GET /api/WaterConsumption/getall`. The GeoJSON contains 119
features; three are malformed (missing name or consumption) and are filtered, leaving 113 rows.
`AverageMonthlyKL` is represented as a string in the source and parsed into `int?`.

No provenance, licence, acquisition date, CRS declaration or quality flag is attached to the source.
The table stores styling values (`Stroke`, `Fill`, etc.) alongside data, mixing presentation and
domain concerns.

## 6. UI/layout assessment

- `_Layout.cshtml` uses AdminLTE 2 with a logo header, minimal sidebar and a content wrapper.
- `homemap.css` sets `#map { height: calc(100vh - 0px) }` and `.leaflet-container { height: 100vh }`.
  The map therefore overflows the content wrapper and the page scrolls; the header is obscured.
- The layer control is absolutely positioned at `top: 500px`, which breaks at smaller heights.
- The chart overlay is a fixed `30vw x 48vh` panel that obscures the map on laptops.
- The sidebar is not responsive and contains one link. The header is empty apart from branding.
- The map does not expose coordinate search, drawing, measurement, bookmarks, printing, scale,
  north arrow, layer opacity/ordering or feature panels.
- The page uses jQuery and synchronous hover interactions; there is no loading skeleton, empty
  state, error surface or accessible selection workflow.
- Map state is not preserved between views because no other views exist.

## 7. Security findings

1. No authentication or authorization; all data and Swagger are public.
2. CORS is configured with `AllowAnyOrigin/Header/Method`, inappropriate once write APIs exist.
3. No CSRF protection; no state-changing APIs exist yet, but this must change.
4. No request validation, rate limiting, upload validation or audit trail.
5. Development exception page and MiniProfiler are enabled only in Development, which is correct,
   but production configuration is otherwise absent.
6. Connection string and credentials are checked into `appsettings.json` and `database/setup.sql`.
7. No HTTPS/HSTS enforcement in production beyond defaults; no security headers.
8. Consumer/personally identifiable data is out of scope today but must be protected in the target.

## 8. Performance and data-integrity findings

- `GetAll` loads every row into memory on each request and triggers an import check (`Any()`).
- `DatabaseContext` is registered as `Transient`; scoped is appropriate for web requests.
- No pagination, filtering, projection or `AsNoTracking()`.
- No spatial index; geometry is opaque JSON text.
- Import is non-transactional: a failure halfway leaves a partially populated table.
- The importer mutates parsed strings with `Replace(".0", "")`, which can corrupt values such as
  `10.05`.
- There are no uniqueness constraints, concurrency tokens or audit fields.
- MiniProfiler and EF logging are wired for development; no structured production logging.

## 9. Test assessment

The four NUnit tests cover:

- import of the Cape Town file and filtering of malformed rows (113 valid rows);
- top-ten ordering and null exclusion;
- filtering of null rows already persisted;
- missing GeoJSON file handling.

They use EF Core InMemory, so they do not verify PostgreSQL migrations, PostGIS behaviour, HTTP
authorization, CSRF, spatial queries or browser interactions. Existing README instructions are
consistent with these tests. The working tree also contains an uncommitted test project, `.env.example`,
`README.md`, `DEMO_GUIDE.md`, `database/setup.sql` and `.sln` modifications that predate this
implementation.

## 10. Reuse decisions

| Existing asset | Decision |
|---|---|
| ASP.NET Core 8 MVC | **Retain.** Extend with APIs, Identity and new views. |
| EF Core + Npgsql | **Retain.** Add NetTopologySuite/PostGIS mapping and migrations. |
| Leaflet bundle | **Retain.** Reuse CSS/JS; add draw plugin. |
| AdminLTE/Bootstrap 3 | **Retain as bundled legacy assets** but replace the application shell with a purpose-built responsive GIS layout. Old assets remain available for the legacy page. |
| Chart.js 2 | Replace on new pages with a current, locally vendored Chart.js 4 build; keep old file for compatibility. |
| Legacy `Consumptions` pipeline | **Retain read-only for backward compatibility**, but remove it from the product navigation once the Port Harcourt consumption model is live. Mark as legacy in README. |
| Repository pattern | Retain and extend, but move calculations into domain services and expose typed DTOs. |
| Swagger | Retain and document new APIs. |

## 11. Risks and constraints

- No authoritative Port Harcourt utility asset register, DMA boundaries or consumer records exist in
  the repository. These must be imported by an authorised operator or represented as clearly
  labelled demonstration data.
- Official NIHSA flood products may require licensing/technical access; real-time integration cannot
  be assumed.
- Publicly available OSM water infrastructure in the study area is sparse (six water towers at the
  time of extraction). This is a source-coverage limitation, not a software defect.
- Nigeria's 2006 census-based state projections are the available official population figures; the
  COD-PS package exposes admin-1 totals, not LGA totals. Per-capita metrics are therefore only
  calculated where a population value exists.
- The development machine has PostgreSQL 18 with PostGIS 3.6 available but not yet enabled in the
  application database. This is a deployment prerequisite, not an application design assumption.

## 12. Audit conclusion

The existing application is a working single-purpose demonstration. Its only reusable production
patterns are the ASP.NET Core/EF Core/PostgreSQL stack and the Leaflet map. A Port Harcourt water
intelligence platform requires a new spatial schema, typed domain model, authenticated APIs,
ingestion pipeline, analytical services, operational workflows and a redesigned shell. The target
architecture is recorded in `docs/architecture/waterwatch-target-architecture.md`; the
dependency-ordered work is in `docs/planning/waterwatch-implementation-backlog.md`.
