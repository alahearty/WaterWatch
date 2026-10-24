"""Prepare the checked-in Port Harcourt spatial source extracts.

Inputs (downloaded separately, see docs/data/waterwatch-spatial-data-inventory.md):
  * OCHA Nigeria COD-AB GeoJSON archive (CC BY-IGO)
  * Overpass API JSON extract for the Port Harcourt / Obio-Akpor study area (ODbL)

This script is deterministic apart from source download timestamps. It writes
derived GeoJSON files into data/sources. No geometry is invented.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data" / "sources"
OUT.mkdir(parents=True, exist_ok=True)

RAW = Path(os.environ.get("WATERWATCH_RAW_DATA", Path.home() / "waterwatch-raw"))
OCHA_DIR = RAW / "nga"
OVERPASS_JSON = RAW / "osm_ph.json"


def load_json(path: Path) -> dict:
    # PowerShell's Out-File may prepend a BOM; tolerate it.
    with path.open(encoding="utf-8-sig") as handle:
        return json.load(handle)


def write_geojson(name: str, features: list[dict]) -> None:
    collection = {"type": "FeatureCollection", "features": features}
    with (OUT / name).open("w", encoding="utf-8") as handle:
        json.dump(collection, handle, ensure_ascii=False, separators=(",", ":"))


def select_admin(level: str, predicate) -> list[dict]:
    data = load_json(OCHA_DIR / f"nga_{level}.geojson")
    return [feature for feature in data["features"] if predicate(feature["properties"])]


def prepare_admin_boundaries() -> None:
    if not OCHA_DIR.exists():
        print(f"SKIP admin boundaries: {OCHA_DIR} not found", file=sys.stderr)
        return

    write_geojson(
        "nga_admin0.geojson",
        select_admin("admin0", lambda p: p.get("adm0_name") == "Nigeria"),
    )
    write_geojson(
        "rivers_admin1.geojson",
        select_admin("admin1", lambda p: p.get("adm1_name") == "Rivers"),
    )
    write_geojson(
        "port_harcourt_lgas.geojson",
        select_admin(
            "admin2",
            lambda p: p.get("adm2_name") in {"Port-Harcourt", "Obia/Akpor"},
        ),
    )
    nearby = {
        "Ikwerre",
        "Etche",
        "Eleme",
        "Oyigbo",
        "Okrika",
        "Degema",
        "Emohua",
    }
    write_geojson(
        "rivers_nearby_lgas.geojson",
        select_admin(
            "admin2",
            lambda p: p.get("adm1_name") == "Rivers" and p.get("adm2_name") in nearby,
        ),
    )
    print("Prepared OCHA administrative boundaries")


def osm_feature(element: dict) -> dict | None:
    tags = element.get("tags", {})
    geometry = element.get("geometry")
    element_type = element["type"]
    osm_id = element["id"]

    if element_type == "node":
        if "lat" not in element:
            return None
        geom = {"type": "Point", "coordinates": [element["lon"], element["lat"]]}
    elif element_type == "way":
        if not geometry:
            return None
        coords = [[point["lon"], point["lat"]] for point in geometry]
        is_area = (
            tags.get("natural") == "water"
            or tags.get("landuse") == "reservoir"
            or tags.get("man_made") in {"water_tower", "water_works", "reservoir_covered"}
        )
        if is_area and len(coords) >= 4 and coords[0] == coords[-1]:
            geom = {"type": "Polygon", "coordinates": [coords]}
        else:
            geom = {"type": "LineString", "coordinates": coords}
    else:
        # Relation geometry requires multipolygon assembly. We retain the
        # authoritative centroid and mark it as a centroid representation.
        center = element.get("center")
        if not center:
            return None
        geom = {"type": "Point", "coordinates": [center["lon"], center["lat"]]}
        tags = {**tags, "waterwatch_geometry": "centroid_of_relation"}

    return {
        "type": "Feature",
        "id": f"osm/{element_type}/{osm_id}",
        "geometry": geom,
        "properties": {
            **tags,
            "osm_type": element_type,
            "osm_id": osm_id,
            "osm_url": f"https://www.openstreetmap.org/{element_type}/{osm_id}",
        },
    }


def classify(tags: dict) -> str | None:
    if tags.get("man_made") in {
        "water_tower",
        "water_works",
        "pumping_station",
        "water_well",
        "borehole",
        "reservoir_covered",
        "water_tap",
        "pipeline",
    }:
        return "water_infrastructure"
    if tags.get("amenity") in {"drinking_water", "water_point"}:
        return "water_infrastructure"
    if tags.get("emergency") == "fire_hydrant":
        return "water_infrastructure"
    if tags.get("waterway") in {"river", "stream", "canal", "drain", "ditch", "dam", "weir"}:
        return "waterways"
    if tags.get("natural") == "water" or tags.get("landuse") == "reservoir":
        return "water_bodies"
    if tags.get("place") in {"suburb", "neighbourhood", "quarter", "village", "town"}:
        return "settlements"
    return None


def prepare_osm_layers() -> None:
    if not OVERPASS_JSON.exists():
        print(f"SKIP OSM layers: {OVERPASS_JSON} not found", file=sys.stderr)
        return

    buckets: dict[str, list[dict]] = {
        "water_infrastructure": [],
        "waterways": [],
        "water_bodies": [],
        "settlements": [],
    }
    for element in load_json(OVERPASS_JSON)["elements"]:
        layer = classify(element.get("tags", {}))
        if not layer:
            continue
        feature = osm_feature(element)
        if feature:
            feature["properties"]["waterwatch_layer"] = layer
            buckets[layer].append(feature)

    write_geojson("osm_water_infrastructure.geojson", buckets["water_infrastructure"])
    write_geojson("osm_waterways.geojson", buckets["waterways"])
    write_geojson("osm_water_bodies.geojson", buckets["water_bodies"])
    write_geojson("osm_settlements.geojson", buckets["settlements"])
    for name, features in buckets.items():
        print(f"Prepared OSM {name}: {len(features)} features")


if __name__ == "__main__":
    prepare_admin_boundaries()
    prepare_osm_layers()
