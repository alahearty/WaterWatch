# WaterWatch — Implementation Backlog

Last updated: 2026-10-23  
Status legend: NOT STARTED · IN PROGRESS · IMPLEMENTED — UNVERIFIED · VERIFIED · BLOCKED

The backlog is dependency ordered. Work items are implemented as vertical slices (database →
service → API → UI → tests) and remain runnable throughout.

## Slice A — Foundation

| ID | Item | Depends on | Acceptance |
|---|---|---|---|
| A1 | Existing-system audit and target architecture | — | Audit, architecture, inventory documents complete. |
| A2 | Licensed Port Harcourt source extracts (OCHA boundaries, OSM waterways/assets) | A1 | Files in `data/sources`, provenance recorded, reproducibility script. |
| A3 | PostGIS database and EF baseline | A2 | `waterwatch` DB with PostGIS; migration applies cleanly; legacy table preserved. |
| A4 | Geographic configuration and boundary import | A3 | LGA/state/country records with geometry, GiST indexes, 4326; initial map extent from boundaries. |
| A5 | Identity, roles, policies, audit, rate limiting | A3 | Login works; role restrictions enforced by API tests; mutations audited. |
| A6 | Spatial layer catalogue and GeoJSON API | A4 | Layer list, per-layer GeoJSON, bbox/filters, source/licence metadata. |
| A7 | Ingestion pipeline (GeoJSON/CSV/KML; SHP/GPKG optional) | A4 | Preview, validation, dedupe, transactional commit, error report. |

## Slice B — GIS and infrastructure

| ID | Item | Depends on | Acceptance |
|---|---|---|---|
| B1 | Enterprise shell and responsive sidebar/topbar | A5 | All navigation routes exist; no dead links; viewport fills screen. |
| B2 | GIS Explorer | A6, B1 | Basemaps, layer tree, opacity/order, popups, search, coordinate search, bookmarks, measure, draw/edit, buffer, export, print, scale, north arrow, coordinates, fullscreen, reset/fit, legend. |
| B3 | Asset registry | A5, B2 | 15 asset types; CRUD with permissions; map create; filters/search; detail; documents; audit. |
| B4 | Network nodes/edges and tracing | B3 | Verified connectivity only; upstream/downstream/isolation endpoints. |
| B5 | OSM asset/waterway import | A7, B3 | Real OSM features imported with source and geometry; no fabricated assets. |

## Slice C — Measurements

| ID | Item | Depends on | Acceptance |
|---|---|---|---|
| C1 | Consumer/meter registry | A3, A5 | Account/meter records with sensitivity controls. |
| C2 | Meter readings and CSV import | C1 | Validation, rollover/correction, duplicates, import report. |
| C3 | Consumption analytics | C2 | Aggregates by LGA/zone/DMA/period; per-capita when population exists; trends/anomalies; choropleth; units. |
| C4 | Water quality stations/campaigns/samples/results | A5 | Result entry; configurable standards; four classification states. |
| C5 | Water quality analytics | C4 | Trends, hotspots, exceedances, reports. |
| C6 | Hydrology stations/observations + Open-Meteo sync | A4 | Real rainfall provenance; manual water-level/flow observations; charts. |
| C7 | Groundwater registry/details/readings/pumping tests | B3 | Borehole-specific attributes and trends; observed vs inferred aquifer flag. |
| C8 | NRW DMA balances and leak incidents | C1, B3 | Documented water balance; unallocated state; loss trend; work-order link. |

## Slice D — Risk and operations

| ID | Item | Depends on | Acceptance |
|---|---|---|---|
| D1 | Flood layers/events and exposure analysis | A6, B3 | Five layer types; classification/metadata; asset exposure; no false forecast claims. |
| D2 | Remote sensing STAC catalogue and slider | A5 | Real Sentinel-2/Landsat metadata; cloud/date/resolution shown; failure state. |
| D3 | IoT devices, ingestion, alerts, SignalR | A5, B3 | Key-authenticated ingest; validation; offline/freshness; demo mode labelled. |
| D4 | Field operations work orders | A5, B3 | Reported→Triaged→Assigned→InProgress→Resolved→Verified→Closed; authorization. |
| D5 | Spatial analytics services | A6, B3 | point-in-polygon, buffer, nearest, coverage, density, hotspots, exposure; GiST usage; limits. |
| D6 | Forecasting baselines | C3, C5, C6 | Method/version/coverage/metrics exposed; minimum data threshold. |
| D7 | Dashboards and map-linked filters | B1 onwards | Functional KPIs/charts/map; availability states; click-through filters. |
| D8 | Reports (CSV/XLSX/PDF) | D7 | Selected records, filters, metadata, download. |

## Slice E — Hardening and acceptance

| ID | Item | Depends on | Acceptance |
|---|---|---|---|
| E1 | Domain/API integration tests | A5–D8 | Calculations, auth, spatial queries, transitions, imports, reports. |
| E2 | End-to-end critical workflow checks | E1 | 14 acceptance scenarios from the assignment documented with evidence. |
| E3 | Documentation and deployment | E1 | README, env example, migration/ingestion/API/admin/user/deploy/backup docs. |
| E4 | Known limitations register and final report | E3 | Honest status, dependencies and evidence. |

## Explicit external dependencies

| Dependency | Impact | Mitigation |
|---|---|---|
| Authoritative Port Harcourt utility asset register, DMA boundaries, consumer accounts | Real utility KPIs cannot be produced | Full import/CRUD support; demonstration mode visibly labelled; data-availability states. |
| Official NIHSA flood feed/licence | No official operational flood warning | Flood module accepts official layers; unvalidated demo susceptibility clearly labelled. |
| LGA population statistics | Per-capita only at state level | Admin-area population field/import; metrics show "population unavailable". |
| Physical IoT devices | No real telemetry | Device API + demo generator; all synthetic rows labelled. |
| Shapefile/GeoPackage native readers | Format support depends on optional packages | GeoJSON/CSV/KML supported in-core; unsupported formats return explicit errors. |

## Status board (updated during implementation)

| Slice | Status | Evidence |
|---|---|---|
| A1 | VERIFIED | Audit/architecture/inventory/backlog files created; repository inspected. |
| A2 | VERIFIED | `data/sources/*.geojson`; `scripts/prepare_spatial_sources.py`; OCHA/OSM licence notes. |
| A3–A7 | PENDING | — |
| B1–B5 | PENDING | — |
| C1–C8 | PENDING | — |
| D1–D8 | PENDING | — |
| E1–E4 | PENDING | — |
