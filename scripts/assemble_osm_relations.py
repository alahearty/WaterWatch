"""Assemble OSM multipolygon relations into GeoJSON polygons.

The Overpass extract for relations includes member way geometry. We reconstruct
outer/inner rings by chaining member ways on shared coordinates. This is a small,
dependency-free implementation for the Port Harcourt extraction; it does not
attempt to replace a full OSM multipolygon assembler.
"""
from __future__ import annotations

import json
import math
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data" / "sources"
RAW = Path(os.environ.get("WATERWATCH_RAW_DATA", Path.home() / "waterwatch-raw"))
RELATIONS = RAW / "osm_ph_relations.json"

EPSILON = 1e-9


def equal(a, b) -> bool:
    return abs(a[0] - b[0]) <= EPSILON and abs(a[1] - b[1]) <= EPSILON


def chain_ways(ways: list[list[list[float]]]) -> list[list[list[float]]]:
    remaining = [list(w) for w in ways if len(w) >= 2]
    rings: list[list[list[float]]] = []
    while remaining:
        current = remaining.pop(0)
        changed = True
        while changed and not equal(current[0], current[-1]):
            changed = False
            for index, candidate in enumerate(remaining):
                if equal(current[-1], candidate[0]):
                    current.extend(candidate[1:])
                    remaining.pop(index)
                    changed = True
                    break
                if equal(current[-1], candidate[-1]):
                    current.extend(reversed(candidate[:-1]))
                    remaining.pop(index)
                    changed = True
                    break
                if equal(current[0], candidate[-1]):
                    current = candidate[:-1] + current
                    remaining.pop(index)
                    changed = True
                    break
                if equal(current[0], candidate[0]):
                    current = list(reversed(candidate[1:])) + current
                    remaining.pop(index)
                    changed = True
                    break
        if len(current) >= 4 and equal(current[0], current[-1]):
            rings.append(current)
    return rings


def point_in_ring(point, ring) -> bool:
    x, y = point
    inside = False
    j = len(ring) - 1
    for i in range(len(ring)):
        xi, yi = ring[i][0], ring[i][1]
        xj, yj = ring[j][0], ring[j][1]
        if ((yi > y) != (yj > y)) and (
            x < (xj - xi) * (y - yi) / ((yj - yi) or 1e-15) + xi
        ):
            inside = not inside
        j = i
    return inside


def assemble_multipolygon(members: list[dict]) -> dict | None:
    outer_ways = [
        [[p["lon"], p["lat"]] for p in m.get("geometry", [])]
        for m in members
        if m.get("role") == "outer"
    ]
    inner_ways = [
        [[p["lon"], p["lat"]] for p in m.get("geometry", [])]
        for m in members
        if m.get("role") == "inner"
    ]
    outers = chain_ways(outer_ways)
    inners = chain_ways(inner_ways)
    if not outers:
        return None

    polygons = []
    for outer in outers:
        holes = [inner for inner in inners if point_in_ring(inner[0], outer)]
        polygons.append([outer, *[list(hole) for hole in holes]])
    if len(polygons) == 1:
        return {"type": "Polygon", "coordinates": polygons[0]}
    return {"type": "MultiPolygon", "coordinates": polygons}


def relation_geometry(members: list[dict]) -> dict | None:
    outer = assemble_multipolygon(members)
    if outer:
        return outer
    # Waterway relations are usually collections of way segments; emit one
    # MultiLineString while retaining member-level provenance in properties.
    lines = []
    for member in members:
        geometry = member.get("geometry", [])
        if len(geometry) >= 2:
            lines.append([[p["lon"], p["lat"]] for p in geometry])
    return {"type": "MultiLineString", "coordinates": lines} if lines else None


def run() -> None:
    if not RELATIONS.exists():
        print(f"SKIP relation assembly: {RELATIONS} not found", file=sys.stderr)
        return

    elements = json.loads(RELATIONS.read_text(encoding="utf-8-sig"))["elements"]
    water_body_features = []
    waterway_features = []
    for element in elements:
        tags = element.get("tags", {})
        members = element.get("members", [])
        geometry = relation_geometry(members)
        if not geometry:
            continue
        props = {
            **tags,
            "osm_type": "relation",
            "osm_id": element["id"],
            "osm_url": f"https://www.openstreetmap.org/relation/{element['id']}",
            "waterwatch_geometry": "assembled_from_member_ways",
            "member_count": len(members),
        }
        feature = {
            "type": "Feature",
            "id": f"osm/relation/{element['id']}",
            "geometry": geometry,
            "properties": props,
        }
        if tags.get("waterway"):
            props["waterwatch_layer"] = "waterways"
            waterway_features.append(feature)
        else:
            props["waterwatch_layer"] = "water_bodies"
            water_body_features.append(feature)

    # Merge with the way/node-derived layers, replacing centroid placeholders.
    def read(name):
        path = OUT / name
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"features": []}

    bodies = read("osm_water_bodies.geojson")
    body_features = [
        f
        for f in bodies["features"]
        if f["properties"].get("waterwatch_geometry") != "centroid_of_relation"
    ]
    waterways = read("osm_waterways.geojson")
    way_features = [
        f
        for f in waterways["features"]
        if f["properties"].get("waterwatch_geometry") != "centroid_of_relation"
    ]

    body_features.extend(water_body_features)
    way_features.extend(waterway_features)
    (OUT / "osm_water_bodies.geojson").write_text(
        json.dumps({"type": "FeatureCollection", "features": body_features}, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    (OUT / "osm_waterways.geojson").write_text(
        json.dumps({"type": "FeatureCollection", "features": way_features}, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    print(f"Assembled {len(water_body_features)} water-body relations and {len(waterway_features)} waterway relations")


if __name__ == "__main__":
    run()
