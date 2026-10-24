# WaterWatch — Spatial Data Inventory

Last updated: 2026-10-23  
Study area: Port Harcourt City LGA and Obio/Akpor LGA, Rivers State, Nigeria.  
Initial viewport centre: **4.8156, 7.0498** (not an administrative or surveyed asset location).

## 1. Storage and projection rules

| Concern | Value |
|---|---|
| Geographic storage/interchange | EPSG:4326 (WGS 84), GeoJSON RFC 7946 |
| Web map display | EPSG:3857 (Web Mercator) via Leaflet/tile services |
| Metric area/distance | EPSG:32632 (WGS 84 / UTM zone 32N) or PostGIS geography |
| SRID enforcement | Database geometry columns `geometry(..., 4326)`; imports transformed before commit |
| Spatial indexes | GiST on every geometry column |
| Source geometry fidelity | Never inferred from proximity; OSM relation centroids are explicitly flagged until assembled |

## 2. Checked-in source extracts

These files are in `data/sources` and are the offline baseline used by the seeding/import process.

### 2.1 Administrative boundaries — OCHA Nigeria COD-AB

- **Publisher:** OCHA / Humanitarian Data Exchange (HDX), derived from Nigerian administrative
  sources (`ehealthafrica.org`, source date 2013 in OSM tags; COD `valid_on` 2019-04-17).
- **Dataset:** *Nigeria - Subnational Administrative Boundaries* (`cod-ab-nga`).
- **Licence:** Creative Commons Attribution for Intergovernmental Organisations (CC BY-IGO).
- **Acquired:** 2026-10-23 from
  `https://data.humdata.org/dataset/81ac1d38-f603-4a98-804d-325c658599a3/resource/7e30ec96-7f29-4ee8-9f4c-77633b353cbb/download/nga_admin_boundaries.geojson.zip`.
- **Derived records:**

| File | Features | Geometry | Coverage | Notes |
|---|---:|---|---|---|
| `nga_admin0.geojson` | 1 | MultiPolygon | Nigeria | Country context. |
| `rivers_admin1.geojson` | 1 | Polygon | Rivers State | State context; COD-PS population 7,476,805 (2022 projection). |
| `port_harcourt_lgas.geojson` | 2 | Polygon | Port-Harcourt LGA (NG033022), Obia/Akpor LGA (NG033015) | Initial map extent derives from these two LGAs. |
| `rivers_nearby_lgas.geojson` | 7 | Polygon | Ikwerre, Etche, Eleme, Oyigbo, Okrika, Degema, Emohua | Optional context; not part of initial KPIs. |

- **Attributes retained:** COD names and PCodes (`adm0_*` … `adm2_*`), area, centroid, valid dates.
- **Quality/limitations:** COD geometry is a cartographic boundary, not a cadastral or utility
  boundary. The LGA name in COD is `Obia/Akpor`; the conventional spelling `Obio/Akpor` is kept as
  an alias in the application. Boundaries must not be used for land ownership or billing.
- **Wards:** COD-AB admin3 contains no Rivers State features. GRID3 Operational Wards v3.0 covers 24
  states but **not Rivers**. Ward polygons are therefore unavailable from these sources; the app
  does not invent them.

### 2.2 OpenStreetMap extract — waterways, water bodies, settlements, water infrastructure

- **Publisher:** OpenStreetMap contributors.
- **Licence:** Open Database License (ODbL) 1.0. Attribution: © OpenStreetMap contributors.
- **Extraction:** Overpass API, bounding box `(4.70, 6.90, 4.98, 7.12)`, query timestamp
  **2026-10-23T00:37:05Z**. Raw responses retained outside the repository; reconstruction steps are
  in `scripts/prepare_spatial_sources.py` and `scripts/assemble_osm_relations.py`.
- **Derived records:**

| File | Features | Geometry | Coverage | Quality notes |
|---|---:|---|---|---|
| `osm_waterways.geojson` | 323 | LineString (318), Point (4), MultiLineString (1) | Port Harcourt / Obio-Akpor bbox, including river sections extending outside LGA | Mapped centre-lines only; ditch/drain coverage is incomplete and not an engineering drainage model. |
| `osm_water_bodies.geojson` | 76 | Polygon (73), MultiPolygon (3) | Same bbox | Multipolygon interiors assembled from member ways. Some small features may be missing due to source mapping completeness. |
| `osm_water_infrastructure.geojson` | 6 | Polygon (5 building footprints), Point (1) | Same bbox | All are `man_made=water_tower`; no treatment works, pump stations, boreholes or hydrants were mapped in the extract. Sparse source coverage is explicit. |
| `osm_settlements.geojson` | 82 | Point | Same bbox | Place nodes for suburb/neighbourhood/quarter/village/town. Points represent settlement labels, not settlement polygons. |

- **Tag preservation:** Original OSM tags are retained in feature properties, plus `osm_type`,
  `osm_id`, `osm_url`, and for relations `waterwatch_geometry = assembled_from_member_ways`.
- **Conflation rule:** OSM water towers are imported as assets with `DataClassification=Imported`
  and `Source=OpenStreetMap`. Assets are never created from proximity or assumption.

### 2.3 Population context — OCHA COD-PS

- **Dataset:** *Nigeria - Subnational Population Statistics* (`cod-ps-nga`).
- **Licence:** CC BY-IGO.
- **Reference:** 2022 projection from the 2006 census (National Population Commission).
- **Available value:** Rivers State total = **7,476,805** (2022 projection).
- **Admin-2 values:** not published in the COD-PS package (adm0/adm1 only). LGA-level population is
  therefore **not available** from this source. The application stores a nullable population on
  admin areas and displays “population unavailable” rather than estimating LGA per-capita values.
- **WorldPop raster:** not ingested; would require zonal-statistics preprocessing and is out of
  scope for the initial release. Can be added later with recorded parameters.

## 3. Derived/application-generated data

| Dataset | Classification | Origin |
|---|---|---|
| Demonstration service zones and DMAs | `SyntheticDemonstration` | WaterWatch analysis sectors created for exercising workflows; **not official utility boundaries**. Labelled in the UI and never used in official reports without the demo badge. |
| Demonstration flood susceptibility layer | `SyntheticDemonstration` / `ModelledSusceptibility` | Buffer generated from mapped OSM waterways with documented distance and method. **Not an official flood forecast or warning.** |
| Demonstration consumption, quality, hydrology, groundwater, NRW and work-order records | `SyntheticDemonstration` | Generated for workflow demonstration only; flagged in every API response and UI badge. |
| Demo telemetry | `SyntheticDemonstration` | Generated by the demo telemetry service when enabled; never mixed silently with observed readings. |
| Forecast outputs | `Modelled` | Baseline statistical models; method, training window, coverage and metrics recorded. |
| User-entered asset/sample/work-order data | `Observed` | Operator-supplied; provenance (user, timestamp) recorded in audit. |

## 4. Real-time/external services

| Service | Data | Licence/terms | Status |
|---|---|---|---|
| OpenStreetMap tiles | Basemap | ODbL / tile usage policy | Configured; attribution displayed. |
| Esri World Imagery / World Topo | Basemap | Esri terms, attribution required | Configured; no imagery is redistributed. |
| Nominatim | Geocoding search | ODbL; usage policy | Proxied server-side with rate limiting and a contact User-Agent. |
| Open-Meteo | Rainfall observations/forecast | CC BY 4.0 | Implemented server-side; data labelled as external observations/forecasts. |
| Element 84 Earth Search STAC | Sentinel-2/Landsat metadata | Open data (ESA/USGS); service terms | Catalogue search; imagery not copied into the repository. |
| NIHSA | Official flood warnings | Not confirmed | **External dependency**; no feed configured, so the UI shows “official warnings not available”. The application never fabricates an official warning. |

## 5. Data gaps and honest availability states

1. No authoritative utility asset register, DMA boundaries or consumer/meter records.
2. No ward polygons for Rivers State from COD/GRID3.
3. No public water-level/streamflow gauge telemetry for Port Harcourt in this integration.
4. No official NIHSA flood layer or warning feed configured.
5. No LGA population from COD-PS; per-capita metrics are state-level only.
6. OSM water asset coverage is sparse (six water towers).
7. OSM waterways are mapped centrelines, not surveyed drainage assets; tidal drainage is not
   represented by a DEM-derived flow network and must not be treated as such.

## 6. Reproducibility

```powershell
# 1. Download the COD-AB GeoJSON archive and extract to $RAW\nga
# 2. Run the Overpass queries from docs/data/waterwatch-data-acquisition.md (raw JSON in $RAW)
$env:WATERWATCH_RAW_DATA = "C:\path\to\waterwatch-raw"
python scripts\prepare_spatial_sources.py
python scripts\assemble_osm_relations.py
```

The application does not download source data at startup. The checked-in extracts are loaded
through the same validated import pipeline used for operator uploads.
