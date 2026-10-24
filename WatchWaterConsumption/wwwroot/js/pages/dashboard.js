(function () {
  'use strict';
  const auth = document.body.dataset.authenticated === 'true';
  const map = L.map('dashboard-map', { zoomControl: true, attributionControl: true });
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors', maxZoom: 19
  }).addTo(map);
  map.setView([4.8156, 7.0498], 12);

  const adminLayer = L.geoJSON(null, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.04 } }).addTo(map);
  const waterLayer = L.geoJSON(null, { style: { color: '#0284c7', weight: 1.4, fillOpacity: 0 } }).addTo(map);
  const assetLayer = L.layerGroup().addTo(map);
  const zoneLayer = L.geoJSON(null, { style: { color: '#7c3aed', weight: 1.5, dashArray: '6 4', fillOpacity: 0.08 } }).addTo(map);

  async function safe(url, fallback) {
    try { return await WW.get(url); } catch (e) { return fallback; }
  }

  async function init() {
    const [assetStats, consumption, trend, nrw, quality, hydrology, telemetry, workStats, alerts, exceedances] = await Promise.all([
      safe('/api/assets/statistics', null),
      safe('/api/consumption/summary', null),
      safe('/api/consumption/trend', []),
      safe('/api/nrw/summary', null),
      safe('/api/water-quality/summary', null),
      safe('/api/hydrology/summary', null),
      auth ? safe('/api/telemetry/summary', null) : Promise.resolve(null),
      auth ? safe('/api/work-orders/statistics', null) : Promise.resolve(null),
      auth ? safe('/api/telemetry/alerts', []) : Promise.resolve([]),
      safe('/api/water-quality/exceedances', [])
    ]);

    if (assetStats) {
      document.getElementById('kpi-assets').textContent = WW.fmtInt(assetStats.total);
      document.getElementById('kpi-assets-foot').textContent =
        assetStats.withoutGeometry + ' without geometry · ' + (assetStats.byStatus.find(s => s.label === 'Operational')?.count || 0) + ' operational';
    }
    if (consumption) {
      document.getElementById('kpi-consumption').innerHTML = WW.fmt(consumption.totalM3, 0) + ' <small>m³</small>';
      document.getElementById('kpi-consumption-foot').textContent =
        (consumption.firstReading ? 'Coverage ' + consumption.firstReading + ' → ' + consumption.lastReading : consumption.coverageMessage);
    }
    if (nrw) {
      document.getElementById('kpi-nrw').innerHTML = WW.fmt(nrw.nrwPercent, 1) + '<small>%</small>';
      document.getElementById('kpi-nrw-foot').textContent = nrw.balances
        ? WW.fmt(nrw.nrwM3, 0) + ' m³ unaccounted · ' + nrw.openLeaks + ' open leaks'
        : nrw.availabilityMessage;
    }
    if (quality) {
      document.getElementById('kpi-quality').textContent = WW.fmtInt(quality.nonCompliant);
      document.getElementById('kpi-quality-foot').textContent = quality.samples
        ? quality.samples + ' samples · ' + quality.insufficientEvidence + ' insufficient evidence'
        : quality.availabilityMessage;
    }

    const availability = [];
    availability.push({ label: 'Administrative boundaries', ok: true, text: 'OCHA COD-AB · 2 LGAs' });
    availability.push({ label: 'Water infrastructure', ok: (assetStats?.total || 0) > 0, text: (assetStats?.total || 0) + ' assets' });
    availability.push({ label: 'Consumption readings', ok: (consumption?.readings || 0) > 0, text: (consumption?.readings || 0) + ' calculated intervals' });
    availability.push({ label: 'Water quality', ok: (quality?.samples || 0) > 0, text: (quality?.samples || 0) + ' samples' });
    availability.push({ label: 'Hydrology observations', ok: (hydrology?.observations || 0) > 0, text: (hydrology?.observations || 0) + ' observations' });
    availability.push({ label: 'Telemetry', ok: !!telemetry && telemetry.readings > 0, text: telemetry ? telemetry.readings + ' readings' : 'Sign in to view' });
    availability.push({ label: 'Work orders', ok: !!workStats && workStats.total > 0, text: workStats ? workStats.total + ' records' : 'Sign in to view' });
    document.getElementById('availability-list').innerHTML = availability.map(a =>
      '<div class="flex-between" style="padding:7px 0;border-bottom:1px dashed #e7eef5">' +
      '<span class="small"><i class="fa ' + (a.ok ? 'fa-check-circle' : 'fa-minus-circle') + '" style="color:' + (a.ok ? '#1f9d67' : '#8a9aab') + '"></i> ' + WW.escape(a.label) + '</span>' +
      '<span class="tiny muted">' + WW.escape(a.text) + '</span></div>').join('') +
      '<div class="availability mt-2"><i class="fa fa-info-circle"></i><div>Data availability is shown explicitly. Zero values are not used to imply that a measured condition is safe or absent.</div></div>';

    renderTrend(trend || []);
    renderAssetChart(assetStats);
    renderQualityChart(quality);

    const signals = [];
    if (!auth) {
      signals.push('<div class="table-empty"><i class="fa fa-lock"></i>Sign in to view alerts and work orders.</div>');
    } else {
      (alerts || []).filter(a => a.status !== 'Resolved').slice(0, 3).forEach(a => signals.push(
        '<div class="notification"><span class="n-icon"><i class="fa fa-bell"></i></span><span><strong>' + WW.escape(a.ruleName) + '</strong><span>' + WW.escape(a.deviceName) + ' · ' + WW.escape(a.message || '') + '</span></span></div>'));
      if (workStats) {
        signals.push('<div style="padding:10px 12px" class="small muted">' + WW.fmtInt(workStats.open) + ' open work orders · ' + WW.fmtInt(workStats.overdue) + ' overdue</div>');
      }
      if (!signals.length) signals.push(WW.empty('No active signals', 'Alerts and work orders will appear here when they require attention.', 'fa-check'));
    }
    document.getElementById('priority-signals').innerHTML = signals.join('');

    const ex = (exceedances || []).slice(0, 6);
    document.getElementById('recent-quality').innerHTML = ex.length ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Station</th><th>Parameter</th><th>Value</th><th>Sampled</th></tr></thead><tbody>' +
      ex.map(e => '<tr><td>' + WW.escape(e.stationName) + '</td><td>' + WW.escape(e.parameter) + '</td><td>' + WW.escape(e.value ?? '—') + ' ' + WW.escape(e.unit) + '</td><td class="nowrap">' + WW.fmtDate(e.sampledAt) + '</td></tr>').join('') +
      '</tbody></table></div>' : WW.empty('No exceedances', 'No non-compliant sample results are recorded, or water quality has not been assessed.', 'fa-flask');

    await loadMap();
  }

  function renderTrend(trend) {
    if (!trend.length) {
      document.getElementById('trend-wrap').innerHTML = WW.empty('No consumption trend', 'Import meter readings or enable demonstration data to calculate consumption.', 'fa-tint');
      return;
    }
    WW.chart('trend-chart', {
      type: 'line',
      data: {
        labels: trend.map(t => t.period),
        datasets: [WW.lineDataset('Consumption (m³)', trend.map(t => Number(t.consumptionM3)), '#0f8b8d', true)]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: WW.axis(''), y: WW.axis('m³') } }
    });
  }

  function renderAssetChart(stats) {
    if (!stats || !stats.byType.length) {
      document.getElementById('asset-type-chart').parentElement.innerHTML = WW.empty('No assets', 'Register an asset or import an authorised asset dataset.', 'fa-wrench');
      return;
    }
    WW.chart('asset-type-chart', {
      type: 'bar',
      data: {
        labels: stats.byType.map(x => x.label),
        datasets: [{ label: 'Assets', data: stats.byType.map(x => x.count), backgroundColor: '#1d6fb8', borderRadius: 4 }]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: WW.axis(''), y: { ...WW.axis('count'), ticks: { precision: 0 } } } }
    });
  }

  function renderQualityChart(quality) {
    if (!quality || quality.results === 0) {
      document.getElementById('quality-chart').parentElement.innerHTML = WW.empty('Not assessed', 'No sample results have been recorded.', 'fa-flask');
      return;
    }
    WW.chart('quality-chart', {
      type: 'doughnut',
      data: {
        labels: ['Compliant', 'Non-compliant', 'Insufficient evidence', 'Not assessed'],
        datasets: [{ data: [quality.compliant, quality.nonCompliant, quality.insufficientEvidence, quality.notAssessed],
          backgroundColor: ['#1f9d67', '#c0392b', '#b7791f', '#8a9aab'], borderWidth: 0 }]
      },
      options: { responsive: true, maintainAspectRatio: false, cutout: '62%', plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } } } }
    });
  }

  async function loadMap() {
    try {
      const context = await WW.get('/api/geography/context');
      if (context.extent) {
        const bounds = [[context.extent.minLat, context.extent.minLon], [context.extent.maxLat, context.extent.maxLon]];
        map.fitBounds(bounds, { padding: [16, 16] });
      }
    } catch { }
    try {
      const admin = await WW.get('/api/layers/admin-lga/geojson');
      adminLayer.clearLayers(); adminLayer.addData(admin);
      adminLayer.eachLayer(l => { if (l.feature) l.bindPopup(WW.popupHtml(l.feature)); });
    } catch { }
    try {
      const zones = await WW.get('/api/layers/operational-zones/geojson');
      zoneLayer.clearLayers(); zoneLayer.addData(zones);
    } catch { }
    try {
      const waterways = await WW.get('/api/layers/osm-waterways/geojson?limit=1500');
      waterLayer.clearLayers(); waterLayer.addData(waterways);
    } catch { }
    try {
      const assets = await WW.get('/api/layers/assets/geojson?limit=1000');
      L.geoJSON(assets, {
        pointToLayer: (f, latlng) => L.circleMarker(latlng, { radius: 5, color: '#ffffff', weight: 1.5, fillColor: f.properties.isSensitive ? '#c0392b' : '#0284c7', fillOpacity: 0.95 }),
        onEachFeature: (f, layer) => layer.bindPopup(WW.popupHtml(f))
      }).addTo(assetLayer);
    } catch { }
  }

  init();
})();
