# WaterWatch - Academic Demonstration Guide

## 1. What problem does the software solve?

WaterWatch turns a neighbourhood-level GeoJSON water-consumption dataset into an interactive
choropleth map and a top-ten ranking. It makes spatial consumption patterns visible without a
desktop GIS application.

## 2. Why does the problem matter?

Water utilities need to identify high-consumption areas and communicate usage patterns. Maps make
these patterns easier to see than tables, and a ranking highlights the areas that need attention.

## 3. Main objectives

- Import neighbourhood polygons and average monthly consumption from GeoJSON into PostgreSQL.
- Render a colour-coded map of consumption.
- Show suburb, group and consumption details in popups.
- Show the ten highest-consumption neighbourhoods in a chart.
- Handle malformed or missing data without breaking the dashboard.

## 4. Technologies used

| Concern | Technology |
|---|---|
| Runtime | ASP.NET Core 8 MVC |
| Database | PostgreSQL 18, EF Core 8 (Npgsql) |
| Map | Leaflet 1.9 with OpenStreetMap/Esri basemaps |
| Chart | Chart.js 2.4 |
| UI | AdminLTE 2 / Bootstrap 3 bundled assets |
| Tests | NUnit + EF Core InMemory (4 tests) |

## 5. Architecture

```
Browser (Leaflet + Chart.js + jQuery)
  -> HomeController (map page)
  -> /api/WaterConsumption/getall and /topten
      -> WaterConsumptionRepository
          -> EF Core DatabaseContext
              -> PostgreSQL cptwater
  map.geojson -> lazy import on first getAll
```

## 6. Database design

Single entity/table:

| Table | Fields |
|---|---|
| `Consumptions` | Id, Neighbourhood, SuburbGroup, AverageMonthlyKL, Stroke, StrokeWidth, StrokeOpacity, Fill, FillOpacity, Coordinates |

`Coordinates` stores the polygon coordinate JSON. The migration is
`20240529101822_InitialMigration`.

## 7. Main modules

- GeoJSON importer with malformed-row filtering.
- All-consumption API and map.
- Top-ten consumer API and chart.
- Leaflet layer switcher, legend and popups.
- Empty-state and missing-data handling.

## 8. Demonstration sequence

1. Start PostgreSQL and the app at <http://localhost:5099/>.
2. On first load, allow a few seconds for the GeoJSON import.
3. Show the choropleth map and the legend.
4. Hover over a polygon and show the suburb group, neighbourhood and KL value.
5. Switch basemaps with the layer control.
6. Show the top-ten bar chart.
7. Open `/api/WaterConsumption/getall` and `/api/WaterConsumption/topten` in the browser or Swagger
   to show the JSON data and row counts (113 valid map rows, 10 ranked rows).
8. Refresh the page to show that data is served from the database after the first import.

## 9. Data flow

1. `GET /api/WaterConsumption/getall` calls the repository.
2. If the `Consumptions` table is empty, the repository reads `map.geojson` from the content root.
3. Each feature's properties and geometry are inserted; malformed features without a name or
   consumption value are skipped.
4. Queries filter NULL rows and return valid records.
5. The Razor page draws polygons with Leaflet and the top-ten chart with Chart.js.

## 10. Limitations and future improvements

- Public application with no authentication or editing UI.
- One static 2017 dataset; no upload or time-series comparison.
- Chart.js/AdminLTE are old bundled assets.
- Three malformed source features are filtered out.
- Future work: data upload, period comparison, search/filter by suburb, authenticated editing,
  and a modern charting library.
