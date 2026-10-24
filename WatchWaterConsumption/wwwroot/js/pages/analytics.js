(function () {
  'use strict';
  const authenticated = document.getElementById('analytics-permissions')?.dataset.authenticated === 'true';
  const map = L.map('analytics-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  const analysisLayer = L.layerGroup().addTo(map);
  const markerLayer = L.layerGroup().addTo(map);
  let pointMarker = null;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    await loadBase();
    bind();
    if (authenticated) await loadNetworkOptions();
  }

  async function loadBase() {
    try {
      const [ctx, admin, waterways, assets] = await Promise.all([
        WW.get('/api/geography/context'), WW.get('/api/layers/admin-lga/geojson'),
        WW.get('/api/layers/osm-waterways/geojson?limit=1500'), WW.get('/api/layers/assets/geojson?limit=1000')
      ]);
      L.geoJSON(admin, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.02 } }).addTo(map);
      L.geoJSON(waterways, { style: { color: '#0284c7', weight: 1.2, fillOpacity: 0 } }).addTo(map);
      L.geoJSON(assets, { pointToLayer: (f, latlng) => L.circleMarker(latlng, { radius: 4, color: '#fff', weight: 1, fillColor: '#0284c7', fillOpacity: 0.9 }), onEachFeature: (f, l) => l.bindPopup(WW.popupHtml(f)) }).addTo(map);
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [18, 18] });
    } catch { }
  }

  function bind() {
    map.on('click', e => {
      document.getElementById('an-lon').value = e.latlng.lng.toFixed(6);
      document.getElementById('an-lat').value = e.latlng.lat.toFixed(6);
      markerLayer.clearLayers();
      pointMarker = L.circleMarker(e.latlng, { radius: 7, color: '#fff', weight: 2, fillColor: '#6d5bd0', fillOpacity: 0.95 }).addTo(markerLayer);
      runPip();
    });
    document.getElementById('an-pip').addEventListener('click', runPip);
    document.getElementById('an-buffer').addEventListener('click', runBuffer);
    document.getElementById('an-nearest')?.addEventListener('click', runNearest);
    document.getElementById('an-coverage')?.addEventListener('click', runCoverage);
    document.getElementById('an-density')?.addEventListener('click', runDensity);
    document.getElementById('an-trace')?.addEventListener('click', runTrace);
    document.getElementById('an-isolation')?.addEventListener('click', runIsolation);
  }

  function coords() {
    return { lon: Number(document.getElementById('an-lon').value), lat: Number(document.getElementById('an-lat').value) };
  }

  async function runPip() {
    const { lon, lat } = coords();
    try {
      const result = await WW.get('/api/analytics/point-in-polygon?lon=' + lon + '&lat=' + lat);
      document.getElementById('an-point-result').innerHTML =
        '<div class="small mb-1"><strong>' + result.longitude.toFixed(6) + ', ' + result.latitude.toFixed(6) + '</strong> · ' + WW.escape(result.interpretation) + '</div>' +
        (result.lga ? '<div class="kv"><dt>LGA</dt><dd>' + WW.escape(result.lga.name) + ' (' + WW.escape(result.lga.code) + ')</dd></div>' : '') +
        (result.operationalAreas?.length ? '<div class="kv"><dt>Operational areas</dt><dd>' + result.operationalAreas.map(a => WW.escape(a.name)).join('<br>') + '</dd></div>' : '<div class="kv"><dt>Operational areas</dt><dd>None configured at this point</dd></div>') +
        '<div class="kv"><dt>Assets at point</dt><dd>' + result.assetsAtPoint + '</dd></div>' +
        (!result.lga ? '<div class="alert alert-warn mt-2"><i class="fa fa-warning"></i><div>The point is outside the loaded LGA boundaries. It is not assigned to a LGA.</div></div>' : '');
      document.getElementById('an-anchor').value = lat.toFixed(4) + ', ' + lon.toFixed(4);
      if (result.lga) document.getElementById('an-anchor').value += ' · ' + result.lga.name;
    } catch (e) { document.getElementById('an-point-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }

  async function runBuffer() {
    const { lon, lat } = coords();
    const radius = Number(document.getElementById('an-radius').value);
    try {
      const result = await WW.post('/api/analytics/buffer', { longitude: lon, latitude: lat, radiusMeters: radius });
      analysisLayer.clearLayers();
      L.geoJSON(JSON.parse(result.geometryGeoJson), { style: { color: '#6d5bd0', weight: 2, dashArray: '6 4', fillColor: '#8b5cf6', fillOpacity: 0.12 } }).addTo(analysisLayer);
      if (analysisLayer.getBounds().isValid()) map.fitBounds(analysisLayer.getBounds(), { padding: [30, 30] });
      document.getElementById('an-buffer-result').innerHTML =
        '<div class="callout-grid"><div class="callout"><div class="label">Buffer area</div><div class="value">' + WW.fmt(result.bufferAreaSqKm, 3) + ' km²</div></div>' +
        '<div class="callout"><div class="label">Assets</div><div class="value">' + result.assetsInside + '</div></div>' +
        '<div class="callout"><div class="label">Stations / devices</div><div class="value">' + result.samplingStationsInside + ' / ' + result.devicesInside + '</div></div></div>' +
        '<div class="tiny muted mt-1">' + WW.escape(result.method) + '</div>' +
        (result.assetNames?.length ? '<div class="small mt-1">' + result.assetNames.map(WW.escape).join(', ') + '</div>' : '');
    } catch (e) { document.getElementById('an-buffer-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }

  async function runNearest() {
    const { lon, lat } = coords();
    try {
      const data = await WW.get('/api/analytics/nearest-assets?lon=' + lon + '&lat=' + lat + '&limit=8');
      analysisLayer.clearLayers();
      data.forEach(a => {
        L.circleMarker([a.latitude, a.longitude], { radius: 7, color: '#fff', weight: 2, fillColor: '#0f8b8d', fillOpacity: 0.95 }).addTo(analysisLayer)
          .bindPopup('<div class="popup-title">' + WW.escape(a.name) + '</div><div class="popup-row"><span>Distance</span><span>' + WW.fmt(a.distanceMeters, 1) + ' m</span></div>');
        L.polyline([[lat, lon], [a.latitude, a.longitude]], { color: '#0f8b8d', weight: 1, dashArray: '4 4' }).addTo(analysisLayer);
      });
      document.getElementById('an-point-result').innerHTML = '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Asset</th><th>Type</th><th class="num">Distance</th></tr></thead><tbody>' +
        data.map(a => '<tr><td>' + WW.escape(a.name) + '<div class="tiny muted">' + WW.escape(a.assetCode) + '</div></td><td>' + WW.escape(a.assetType) + '</td><td class="num">' + WW.fmt(a.distanceMeters, 1) + ' m</td></tr>').join('') + '</tbody></table></div>';
    } catch (e) { document.getElementById('an-point-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }

  async function runCoverage() {
    const type = document.getElementById('an-coverage-type').value;
    const radius = Number(document.getElementById('an-coverage-radius').value);
    try {
      const result = await WW.get('/api/analytics/coverage?radiusMeters=' + radius + (type ? '&assetType=' + type : ''));
      analysisLayer.clearLayers();
      L.geoJSON(JSON.parse(result.geometryGeoJson), { style: { color: '#1d6fb8', weight: 2, fillColor: '#3b82f6', fillOpacity: 0.15 } }).addTo(analysisLayer);
      if (analysisLayer.getBounds().isValid()) map.fitBounds(analysisLayer.getBounds(), { padding: [30, 30] });
      document.getElementById('coverage-status').textContent = result.coveragePercent + '% of LGA area';
      document.getElementById('an-coverage-result').innerHTML = '<div class="callout-grid"><div class="callout"><div class="label">Coverage</div><div class="value">' + WW.fmt(result.coverageAreaSqKm, 1) + ' km²</div></div><div class="callout"><div class="label">LGA area</div><div class="value">' + WW.fmt(result.totalLgaAreaSqKm, 1) + ' km²</div></div><div class="callout"><div class="label">Assets used</div><div class="value">' + result.assetsUsed + '</div></div></div><div class="tiny muted mt-1">' + WW.escape(result.limitations) + '</div>';
    } catch (e) { document.getElementById('an-coverage-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }

  async function runDensity() {
    const dimension = document.getElementById('an-density-dim').value;
    try {
      const data = await WW.get('/api/analytics/density?dimension=' + dimension);
      document.getElementById('an-density-result').innerHTML = '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Area</th><th class="num">Assets</th><th class="num">Per km²</th></tr></thead><tbody>' +
        data.map(d => '<tr><td>' + WW.escape(d.areaName) + '<div class="tiny muted">' + WW.escape(d.areaType) + ' · ' + WW.fmt(d.areaSqKm, 1) + ' km²</div></td><td class="num">' + d.count + '</td><td class="num">' + (d.perSquareKm != null ? WW.fmt(d.perSquareKm, 2) : '—') + '</td></tr>').join('') + '</tbody></table></div>';
    } catch (e) { document.getElementById('an-density-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }

  async function loadNetworkOptions() {
    try {
      const [nodes, edges] = await Promise.all([WW.get('/api/analytics/network/nodes'), WW.get('/api/analytics/network/edges')]);
      document.getElementById('an-node').innerHTML = nodes.map(n => '<option value="' + n.id + '">' + WW.escape(n.code + ' · ' + n.name) + '</option>').join('') || '<option value="">No nodes</option>';
      document.getElementById('an-edge').innerHTML = edges.map(e => '<option value="' + e.id + '">' + WW.escape(e.code) + '</option>').join('') || '<option value="">No edges</option>';
      if (!nodes.length) {
        document.getElementById('an-trace-result').innerHTML = '<div class="availability"><i class="fa fa-info-circle"></i><div>No network nodes are registered. Verified connectivity is required for tracing.</div></div>';
      }
    } catch (e) { WW.toast(e.message, 'warn', 'Network'); }
  }

  async function runTrace() {
    const node = document.getElementById('an-node').value;
    const direction = document.getElementById('an-direction').value;
    if (!node) return;
    try {
      const result = await WW.get('/api/analytics/network/trace?nodeId=' + node + '&direction=' + direction);
      analysisLayer.clearLayers();
      result.edges.forEach(e => { });
      document.getElementById('an-trace-result').innerHTML = '<div class="small"><strong>' + result.nodes.length + '</strong> nodes · <strong>' + result.edges.length + '</strong> edges · <strong>' + result.assetsAffected.length + '</strong> assets</div><div class="tiny muted mt-1">' + WW.escape(result.limitations) + '</div>';
    } catch (e) { document.getElementById('an-trace-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }

  async function runIsolation() {
    const edge = document.getElementById('an-edge').value;
    if (!edge) return;
    try {
      const result = await WW.get('/api/analytics/network/isolation?edgeId=' + edge);
      document.getElementById('an-isolation-result').innerHTML = '<div class="small"><strong>' + result.isolatedNodes.length + '</strong> isolated nodes · <strong>' + result.isolatedEdges.length + '</strong> isolated edges</div><div class="tiny muted mt-1">' + WW.escape(result.limitations) + '</div>';
    } catch (e) { document.getElementById('an-isolation-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }
})();
