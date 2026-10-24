# WaterWatch — Target Architecture

Status: implementation baseline, 2026-10-23  
Scope: Port Harcourt City LGA and Obio/Akpor LGA, Rivers State, Nigeria; extensible to Rivers State
and Nigeria.

## 1. Architectural principles

1. **One deployable modular monolith.** The existing ASP.NET Core application remains a single web
   process. Domain boundaries are enforced with folders/namespaces and typed services, not
   microservices.
2. **PostgreSQL/PostGIS is the system of record for geometry.** All feature geometry is stored as
   `geometry(…, 4326)` with GiST indexes. Metric operations use geography casts or an explicit
   transform to EPSG:32632 (UTM zone 32N).
3. **Evidence before inference.** Every observation and derived record carries a data
   classification (`Observed`, `Imported`, `Estimated`, `Modelled`, `SyntheticDemonstration`) and,
   where applicable, a data source, model version, assumptions and validation state.
4. **APIs are the integration boundary.** Razor pages and JavaScript call versioned JSON APIs.
   Mutations are authorised on the server with role policies and audited.
5. **Repeatable spatial ingestion.** Imports are previewed, validated, deduplicated, executed in a
   transaction and recorded with source metadata.
6. **No invented operational facts.** Real boundaries and OSM features are imported with
   provenance. Utility-specific zones, consumer accounts, samples and telemetry are either
   operator-supplied or seeded as explicitly labelled demonstration data in a separate mode.
7. **Safe analytics.** Forecasts and flood susceptibility never masquerade as observations or
   official warnings. Each analytical output exposes its method, assumptions and limitations.

## 2. Runtime topology

```
Browser
  ├── Razor pages (server-rendered shell, per-module workspaces)
  ├── Leaflet map + vendored Chart.js
  └── fetch/JSON + SignalR telemetry hub
        │
ASP.NET Core 8 (single process)
  ├── MVC page controllers
  ├── JSON API controllers (validation, role policies, rate limiting)
  ├── Domain services
  │     ├── consumption & meter validation
  │     ├── water-quality classification
  │     ├── NRW water balance
  │     ├── work-order state machine
  │     ├── spatial analysis (PostGIS)
  │     ├── forecasting baselines
  │     └── report generation
  ├── Import pipeline (GeoJSON/CSV/KML/WMS/WFS metadata)
  ├── Background services (demo telemetry, rainfall sync, report jobs)
  └── EF Core 8 + Npgsql + NetTopologySuite
        │
PostgreSQL 18 + PostGIS 3.6
  ├── spatial tables + GiST indexes
  ├── time-series tables + temporal indexes
  ├── audit trail
  └── EF migrations history
```

External, optional integrations:

| Integration | Purpose | Failure behaviour |
|---|---|---|
| OpenStreetMap tile services | Basemaps | Map still shows vector layers; attribution remains. |
| Nominatim (server proxied) | Place search | Search reports provider unavailable; coordinate search still works. |
| Open-Meteo | Rainfall observations/forecast | Hydrology shows source unavailable; no synthetic substitute. |
| Element 84 Earth Search STAC | Sentinel-2/Landsat metadata | Catalogue page reports unavailable; no cached scenes claimed as current. |
| NIHSA | Official flood warnings | Module labels "not configured" until an authorised feed is supplied. |
| OSM/Overpass | Infrastructure/waterway extracts | Checked-in extracts remain usable offline. |

## 3. Domain modules

| Module | Core entities | Primary services / APIs |
|---|---|---|
| Geography | `AdminArea`, `OperationalArea` (service zone, DMA, catchment), `SpatialLayer`, `SpatialFeature`, `DataSource` | layer catalogue, GeoJSON features, boundary search, import |
| Infrastructure | `Asset`, `AssetDocument`, `Inspection`, `MaintenanceRecord`, `NetworkNode`, `NetworkEdge` | CRUD, bbox/type/status filters, network trace, isolation |
| Consumption | `ConsumerAccount`, `Meter`, `MeterReading`, `ConsumptionImport` | reading import, rollover/correction rules, zonal aggregates, anomaly baseline |
| Water quality | `SamplingStation`, `SamplingCampaign`, `WaterSample`, `SampleResult`, `QualityStandard`, `QualityLimit` | result entry, classification, exceedance queries, trends |
| Hydrology | `HydrologyStation`, `HydrologyObservation`, waterway/water-body features, `DrainageObstruction` | observations, rainfall sync, drainage reports |
| Groundwater | `BoreholeDetail`, `PumpingTest`, `GroundwaterReading` | borehole registry/detail, water-level trends, contamination proximity |
| Flood | `FloodEvent`, `FloodLayer`, `FloodExposureResult` | layer classification, asset/area exposure, event timeline |
| NRW | `DmaWaterBalance`, `LeakIncident` | water balance, loss split with evidence states, leak workflows |
| Remote sensing | `SatelliteScene` (cache only) | STAC search, before/after comparison |
| Telemetry | `Device`, `TelemetryReading`, `AlertRule`, `Alert` | ingestion API key, validation, SignalR, alert rules |
| Field operations | `WorkOrder`, `WorkOrderEvent` | enforced workflow, assignment, verification |
| Reporting/audit | `ReportJob`, `AuditEntry` | CSV/XLSX/PDF, audit trail |

## 4. Spatial data model

- Storage/interchange: **EPSG:4326** (`geometry`), WGS 84.
- Web rendering: **EPSG:3857** (Leaflet/tile standard), handled by the client and tile services.
- Metric calculations: **EPSG:32632** (WGS 84 / UTM zone 32N) for Port Harcourt; expressed through
  `ST_Transform` or geography casts. No distance/area is calculated from raw degree values.
- Geometry types: `Point` for assets/stations/devices, `LineString` for pipelines/waterways,
  `MultiPolygon` for admin areas/zones/flood extents, generic `Geometry` only where a layer can
  contain mixed types.
- Every spatial table has a GiST index. Time-series tables have `(entity_id, observed_at)` indexes.
- Initial viewport: centre `(4.8156, 7.0498)`, zoom 13. The initial extent is derived from the
  loaded LGA boundary envelope, not hardcoded as a survey extent.

## 5. Security architecture

- ASP.NET Core Identity (EF stores) with cookie authentication.
- Roles: Administrator, GIS Analyst, Water Utility Engineer, Hydrologist, Environmental Officer,
  Field Inspector, Management Viewer, Public Viewer.
- Policies: asset edit, data import, quality management, hydrology management, field operations,
  user administration, sensitive infrastructure.
- Anonymous read-only access to overview/GIS layers; all writes require authenticated roles.
- Antiforgery header validation for JSON mutations; same-origin CORS; secure cookies; HSTS in
  production; rate-limited APIs; validated uploads; audit trail for all writes.
- Sensitive coordinates (`Asset.IsSensitive`) are excluded from public GeoJSON and detail endpoints.
- No secrets in source; environment variables/User Secrets for connection strings and API keys.

## 6. Ingestion architecture

```
Upload (GeoJSON/CSV/KML/SHP/GPKG) or service registration (WMS/WFS)
  -> parse to candidate features
  -> detect/declare CRS (GeoJSON RFC 7946 = EPSG:4326; others require declaration)
  -> transform to EPSG:4326
  -> validate geometry (non-empty, valid, correct type, bbox sanity)
  -> deduplicate (external id, then geometry hash + name)
  -> preview (counts, rejects, sample attributes)
  -> transactional commit to SpatialFeature/typed entity
  -> record DataSource + import batch + audit entry
```

Shapefile/GeoPackage support requires the optional NTS readers; when they are not deployed the
pipeline returns a clear unsupported-format error instead of silently accepting the file.

## 7. Analytical strategy

- **Consumption:** per-meter cumulative reading deltas; rollover handled only when flagged; missing
  or estimated readings propagate an evidence state. Aggregations are computed in the database.
- **Water quality:** limit comparison is standard-specific and traceable. States are
  `Compliant`, `NonCompliant`, `InsufficientEvidence`, `NotAssessed`; absence of a limit never
  implies safety.
- **NRW:** `NRW = system input − billed authorised consumption`. Apparent/real loss split is only
  reported when both components are evidenced; otherwise the balance is `unallocated`.
- **Forecasting:** ordinary least squares/seasonal naive baselines with training window, data
  coverage and validation metrics exposed. No prediction is produced below a minimum observation
  threshold.
- **Flood:** observed/historical/modelled/forecast/official are distinct layer types. Exposure is
  computed by PostGIS intersection and returns an analysis record with assumptions and timestamp.

## 8. UI architecture

- Left sidebar with the 16 product sections; top toolbar with search, geography context, filters,
  notifications and user menu.
- Main workspace fills `calc(100vh - header)` using CSS grid; no hardcoded pixel viewport heights.
- GIS Explorer is a full-height map with collapsible layer panel, contextual inspector, legend and
  tool strip. Map controls sit in reserved corners; analysis panels collapse.
- Shared components: KPI card, availability badge, data-classification badge, filter bar, chart
  container, data table, empty/error/loading states, form modal.
- Map state (centre, zoom, active layers, filters) is preserved in `sessionStorage` when navigating.
- Accessibility: visible focus, keyboard-operable map tools, ARIA labels, contrast-checked palette.

## 9. Deployment

- Single ASP.NET Core app + PostgreSQL/PostGIS. `database/setup.sql` creates role/database and
  enables PostGIS. EF migrations run at startup (or via a documented CLI command).
- Configuration via environment variables: `ConnectionStrings__DefaultConnection`,
  `ASPNETCORE_ENVIRONMENT`, `WaterWatch__DemoMode`, ingestion/device keys, external API contacts.
- Health endpoint `/health` checks database connectivity and PostGIS availability, plus optional
  integration status.
- Backup/restore documented with `pg_dump`/`pg_restore` and uploads directory backup.

## 10. Migration path from the existing application

1. Add PostGIS to a new `waterwatch` database; keep `cptwater` intact for rollback.
2. Replace the initial migration chain with a reviewed baseline for the new schema. The legacy
   `Consumptions` table remains mapped for read-only compatibility until the Port Harcourt pages
   are verified.
3. Rebuild the shell and home page around Port Harcourt; keep `/api/WaterConsumption/*` available
   but undocumented in navigation.
4. Import OCHA boundaries and OSM extracts as the first spatial foundation.
5. Enable demonstration data only through an explicit mode flag; mark every row.
6. Add tests before switching the default route; run migrations and smoke tests.
