(function () {
  'use strict';
  const canAnalyse = document.getElementById('flood-permissions')?.dataset.canAnalyse === 'true';
  let layers = [];
  const map = L.map('flood-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  const floodLayer = L.layerGroup().addTo(map);

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    await loadMapBase();
    await refresh();
    const legend = L.control({ position: 'bottomright' });
    legend.onAdd = function () {
      const div = L.DomUtil.create('div', 'gis-legend');
      div.innerHTML = '<h4>Flood layer type</h4>' +
        '<div class="legend-row"><span class="legend-swatch" style="background:#1d4ed8"></span><span>Modelled susceptibility</span></div>' +
        '<div class="legend-row"><span class="legend-swatch" style="background:#c0392b"></span><span>Observed</span></div>' +
        '<div class="legend-row"><span class="legend-swatch" style="background:#b7791f"></span><span>Historical</span></div>' +
        '<div class="legend-row"><span class="legend-swatch" style="background:#6d5bd0"></span><span>Forecast</span></div>' +
        '<div class="legend-row"><span class="legend-swatch" style="background:#7f1d1d"></span><span>Official warning</span></div>';
      return div;
    };
    legend.addTo(map);
  }

  async function loadMapBase() {
    try {
      const [ctx, admin, waterways, assets] = await Promise.all([
        WW.get('/api/geography/context'),
        WW.get('/api/layers/admin-lga/geojson'),
        WW.get('/api/layers/osm-waterways/geojson?limit=2000'),
        WW.get('/api/layers/assets/geojson?limit=1000')
      ]);
      L.geoJSON(admin, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.02 } }).addTo(map);
      L.geoJSON(waterways, { style: { color: '#0284c7', weight: 1.2, fillOpacity: 0 } }).addTo(map);
      L.geoJSON(assets, { pointToLayer: (f, latlng) => L.circleMarker(latlng, { radius: 4, color: '#fff', weight: 1, fillColor: '#0284c7', fillOpacity: 0.9 }), onEachFeature: (f, l) => l.bindPopup(WW.popupHtml(f)) }).addTo(map);
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [18, 18] });
    } catch { }
  }

  async function refresh() {
    try {
      const [layerData, events, exposures, geojson] = await Promise.all([
        WW.get('/api/flood/layers'),
        WW.get('/api/flood/events'),
        WW.get('/api/flood/exposures'),
        WW.get('/api/layers/flood-layers/geojson?limit=200')
      ]);
      layers = layerData;
      document.getElementById('f-layers').textContent = WW.fmtInt(layerData.length);
      document.getElementById('f-official').textContent = WW.fmtInt(layerData.filter(l => l.isOfficial).length);
      document.getElementById('f-events').textContent = WW.fmtInt(events.length);
      document.getElementById('f-exposures').textContent = WW.fmtInt(exposures.length);
      renderLayers(layerData);
      renderExposures(exposures);
      renderEvents(events);
      floodLayer.clearLayers();
      L.geoJSON(geojson, {
        style: feature => styleFor(feature.properties.layerType),
        onEachFeature: (feature, layer) => layer.bindPopup(popupFor(feature))
      }).addTo(floodLayer);
      if (floodLayer.getBounds().isValid()) map.fitBounds(floodLayer.getBounds(), { padding: [20, 20] });
      document.getElementById('f-map-note').textContent = layerData.length ? layerData.length + ' layers' : 'No layers configured';
    } catch (e) { WW.toast(e.message, 'error', 'Flood'); }
  }

  function styleFor(type) {
    switch (type) {
      case 'Observed': return { color: '#c0392b', weight: 2, fillColor: '#ef4444', fillOpacity: 0.3 };
      case 'Historical': return { color: '#b7791f', weight: 2, fillColor: '#f59e0b', fillOpacity: 0.25 };
      case 'Forecast': return { color: '#6d5bd0', weight: 2, dashArray: '7 4', fillColor: '#8b5cf6', fillOpacity: 0.2 };
      case 'OfficialWarning': return { color: '#7f1d1d', weight: 3, fillColor: '#dc2626', fillOpacity: 0.3 };
      default: return { color: '#1d4ed8', weight: 2, dashArray: '5 4', fillColor: '#3b82f6', fillOpacity: 0.18 };
    }
  }

  function popupFor(feature) {
    const p = feature.properties || {};
    return '<div class="popup-title">' + WW.escape(p.name || 'Flood layer') + '</div>' +
      '<div class="popup-row"><span>Type</span><span>' + WW.escape(p.layerType) + '</span></div>' +
      '<div class="popup-row"><span>Official</span><span>' + (p.isOfficial ? 'Yes' : 'No') + '</span></div>' +
      '<div class="popup-row"><span>Validation</span><span>' + WW.escape(p.validationStatus || 'Not stated') + '</span></div>' +
      '<div class="popup-row"><span>Class</span><span>' + WW.escape(p.classification) + '</span></div>' +
      '<div class="tiny mt-1">' + WW.escape(p.assumptions || '') + '</div>';
  }

  function renderLayers(items) {
    document.getElementById('flood-layer-list').innerHTML = items.length ? '<div style="padding:8px 12px">' + items.map(l =>
      '<div class="callout mb-1"><div class="flex-between"><strong class="small">' + WW.escape(l.name) + '</strong><span class="badge ' + (l.isOfficial ? 'badge-red' : 'badge-blue') + '">' + WW.escape(l.layerType) + '</span></div>' +
      '<div class="tiny muted mt-1">' + WW.escape(l.description || '') + '</div>' +
      '<div class="flex gap-1 flex-wrap mt-1">' + WW.classification(l.dataClassification) + (l.isOfficial ? '<span class="badge badge-red">Official</span>' : '<span class="badge badge-grey">Not official</span>') + '<span class="badge badge-outline">' + WW.escape(l.validationStatus || 'No validation') + '</span></div>' +
      '<div class="tiny muted mt-1"><strong>Assumptions:</strong> ' + WW.escape(l.assumptions || 'Not recorded') + '</div>' +
      '<div class="tiny muted"><strong>Uncertainty:</strong> ' + WW.escape(l.uncertainty || 'Not recorded') + '</div>' +
      '<div class="tiny mt-1" style="color:' + (l.isOperationalWarning ? '#067647' : '#b54708') + '">' + WW.escape(l.warningEligibilityNote) + '</div>' +
      (canAnalyse ? '<button class="btn btn-sm mt-1" data-analyse="' + l.id + '"><i class="fa fa-cubes"></i> Analyse exposure</button>' : '') + '</div>').join('') + '</div>'
      : WW.empty('No flood layers', 'Register an official layer, import a validated product, or enable demonstration data.', 'fa-warning');
    document.querySelectorAll('[data-analyse]').forEach(btn => btn.addEventListener('click', () => analyse(btn.dataset.analyse)));
  }

  async function analyse(layerId) {
    try {
      const result = await WW.post('/api/flood/layers/' + layerId + '/analyse', {});
      WW.toast('Exposure analysis complete.', 'warn', 'Flood');
      await refresh();
      showExposure(result);
    } catch (e) { WW.toast(e.message, 'error', 'Exposure analysis'); }
  }

  function renderExposures(items) {
    document.getElementById('flood-exposures').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Layer</th><th class="num">Assets</th><th class="num">Stations</th><th class="num">Devices</th><th>Analysed</th></tr></thead><tbody>' +
        items.map(e => '<tr class="clickable" data-exposure="' + e.id + '"><td><strong>' + WW.escape(e.floodLayerName) + '</strong><div class="tiny muted">' + WW.classification(e.dataClassification) + '</div></td><td class="num">' + e.assetsExposed + '</td><td class="num">' + e.samplingStationsExposed + '</td><td class="num">' + e.devicesExposed + '</td><td>' + WW.fmtDate(e.analysedAt, true) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No exposure analyses', 'Run an analysis against a flood layer.', 'fa-cubes');
    document.querySelectorAll('[data-exposure]').forEach(row => row.addEventListener('click', async () => {
      try {
        const items = await WW.get('/api/flood/exposures');
        const result = items.find(x => x.id === row.dataset.exposure);
        if (result) showExposure(result);
      } catch { }
    }));
  }

  function showExposure(result) {
    document.getElementById('exposure-body').innerHTML =
      '<div class="alert alert-warn"><i class="fa fa-exclamation-triangle"></i><div>' + WW.escape(result.limitations) + '</div></div>' +
      '<div class="grid grid-3 mb-2"><div class="callout"><div class="label">Assets exposed</div><div class="value">' + result.assetsExposed + '</div></div>' +
      '<div class="callout"><div class="label">Stations exposed</div><div class="value">' + result.samplingStationsExposed + '</div></div>' +
      '<div class="callout"><div class="label">Devices exposed</div><div class="value">' + result.devicesExposed + '</div></div></div>' +
      '<div class="small mb-2"><strong>Layer:</strong> ' + WW.escape(result.floodLayerName) + ' · <strong>Method:</strong> ' + WW.escape(result.method) + ' · ' + WW.fmtDate(result.analysedAt, true) + '</div>' +
      (result.assets?.length ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Asset</th><th>Type</th><th>Status</th></tr></thead><tbody>' + result.assets.map(a => '<tr><td>' + WW.escape(a.name) + '<div class="tiny muted">' + WW.escape(a.assetCode) + '</div></td><td>' + WW.escape(a.assetType) + '</td><td>' + WW.status(a.status) + '</td></tr>').join('') + '</tbody></table></div>' : '') +
      (result.otherLayers?.length ? '<div class="mt-2 small"><strong>Other intersecting layers:</strong> ' + result.otherLayers.map(l => WW.escape(l.layerName) + ' (' + l.count + ')').join(', ') + '</div>' : '');
    WW.modal.open('exposure-modal');
  }

  function renderEvents(items) {
    document.getElementById('flood-events').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Event</th><th>Severity</th><th>Started</th><th>Ended</th><th>Official</th></tr></thead><tbody>' +
        items.map(e => '<tr><td><strong>' + WW.escape(e.name) + '</strong><div class="tiny muted">' + WW.escape(e.description || '') + '</div></td><td>' + WW.status(e.severity) + '</td><td>' + WW.fmtDate(e.startedAt) + '</td><td>' + WW.fmtDate(e.endedAt) + '</td><td>' + (e.isOfficial ? '<span class="badge badge-red">Official</span>' : '<span class="badge badge-grey">No</span>') + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No flood events', 'Historical or observed flood events will appear here.', 'fa-history');
  }
})();
