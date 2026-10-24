(function () {
  'use strict';
  const canManage = document.getElementById('hydrology-permissions')?.dataset.canManage === 'true';
  const authenticated = document.getElementById('hydrology-permissions')?.dataset.authenticated === 'true';
  let stations = [];
  const map = L.map('hydrology-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  const stationLayer = L.layerGroup().addTo(map);

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    bind();
    await loadMap();
    await refresh();
  }

  function bind() {
    document.getElementById('sync-rainfall')?.addEventListener('click', syncRainfall);
    document.getElementById('report-obstruction')?.addEventListener('click', () => WW.modal.open('obstruction-modal'));
    document.getElementById('hyd-station').addEventListener('change', loadObservations);
    document.getElementById('hyd-parameter').addEventListener('change', loadObservations);
    document.getElementById('obstruction-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/hydrology/obstructions', {
          waterwayName: data.get('waterwayName'), description: data.get('description'),
          latitude: data.get('latitude') ? Number(data.get('latitude')) : null,
          longitude: data.get('longitude') ? Number(data.get('longitude')) : null,
          severity: data.get('severity')
        });
        WW.modal.close('obstruction-modal'); WW.toast('Obstruction report submitted.', 'warn', 'Saved'); await refresh();
      } catch (ex) { WW.toast(ex.message, 'error', 'Obstruction'); }
    });
  }

  async function loadMap() {
    try {
      const [waterways, bodies, stationsData, ctx] = await Promise.all([
        WW.get('/api/layers/osm-waterways/geojson?limit=2000'),
        WW.get('/api/layers/osm-water-bodies/geojson?limit=1000'),
        WW.get('/api/hydrology/stations'),
        WW.get('/api/geography/context')
      ]);
      const group = L.geoJSON(waterways, { style: { color: '#0284c7', weight: 1.4, fillOpacity: 0 } }).addTo(map);
      L.geoJSON(bodies, { style: { color: '#0369a1', weight: 1, fillColor: '#7dd3fc', fillOpacity: 0.35 } }).addTo(map);
      stationsData.forEach(s => {
        if (s.longitude != null) {
          L.circleMarker([s.latitude, s.longitude], { radius: 6, color: '#fff', weight: 2, fillColor: '#0369a1', fillOpacity: 0.9 })
            .addTo(stationLayer)
            .bindPopup('<div class="popup-title">' + WW.escape(s.name) + '</div><div class="popup-row"><span>Type</span><span>' + WW.escape(s.stationType) + '</span></div><div class="popup-row"><span>Observations</span><span>' + s.observationCount + '</span></div>');
        }
      });
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [18, 18] });
    } catch (e) { WW.toast(e.message, 'warn', 'Hydrology map'); }
  }

  async function refresh() {
    try {
      const [summary, stationData, obstructions, rain] = await Promise.all([
        WW.get('/api/hydrology/summary'),
        WW.get('/api/hydrology/stations'),
        WW.get('/api/hydrology/obstructions'),
        WW.get('/api/hydrology/rainfall/live').catch(() => null)
      ]);
      stations = stationData;
      document.getElementById('hyd-station').innerHTML = stations.map(s => '<option value="' + s.id + '">' + WW.escape(s.name) + '</option>').join('') || '<option value="">No stations</option>';
      document.getElementById('h-waterways').textContent = WW.fmtInt(summary.waterwayFeatures);
      document.getElementById('h-bodies').textContent = WW.fmtInt(summary.waterBodyFeatures);
      document.getElementById('h-stations').textContent = WW.fmtInt(summary.stations);
      document.getElementById('h-station-types').textContent = summary.rainfallStations + ' rainfall · ' + summary.waterLevelStations + ' water level';
      document.getElementById('h-observations').textContent = WW.fmtInt(summary.observations);
      document.getElementById('h-last').textContent = summary.lastObservationAt ? 'Last ' + WW.fmtRelative(summary.lastObservationAt) : 'No observations';
      document.getElementById('hydrology-availability').innerHTML = '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>' + WW.escape(summary.availabilityMessage) + '</div></div>';
      renderRain(rain);
      renderObstructions(obstructions);
      await loadObservations();
    } catch (e) { WW.toast(e.message, 'error', 'Hydrology'); }
  }

  function renderRain(rain) {
    const status = document.getElementById('rain-status');
    if (!rain || rain.status !== 'available') {
      status.innerHTML = '<div class="alert alert-warn"><i class="fa fa-cloud"></i><div>' + WW.escape(rain?.message || 'Open-Meteo unavailable.') + '</div></div>';
      return;
    }
    status.innerHTML = '<div class="flex-between mb-1"><span class="small">Grid point ' + rain.latitude.toFixed(4) + ', ' + rain.longitude.toFixed(4) + '</span><span class="badge badge-outline">' + WW.escape(rain.source) + '</span></div>' +
      '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>' + WW.escape(rain.message || '') + '</div></div>';
    document.getElementById('rain-note').textContent = rain.licence || '';
    const daily = rain.daily || [];
    if (!daily.length) return;
    WW.chart('rain-chart', {
      type: 'bar',
      data: { labels: daily.map(d => d.date), datasets: [{ label: 'Rainfall (mm)', data: daily.map(d => Number(d.rainfallMm)), backgroundColor: '#1d6fb8', borderRadius: 3 }] },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: { ...WW.axis(''), ticks: { maxRotation: 45, autoSkip: true, maxTicksLimit: 8 } }, y: WW.axis('mm') } }
    });
  }

  async function loadObservations() {
    const stationId = document.getElementById('hyd-station').value;
    if (!stationId) { document.getElementById('hyd-trend-wrap').innerHTML = WW.empty('No station', 'Create or seed a hydrology station.', 'fa-area-chart'); return; }
    const parameter = document.getElementById('hyd-parameter').value;
    try {
      const data = await WW.get('/api/hydrology/observations?stationId=' + stationId + '&parameter=' + parameter + '&limit=500');
      document.getElementById('observation-list').innerHTML = data.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Observed</th><th class="num">Value</th><th>Unit</th><th>Flag</th><th>Class</th></tr></thead><tbody>' +
          data.slice(0, 100).map(o => '<tr><td>' + WW.fmtDate(o.observedAt, true) + '</td><td class="num">' + WW.fmt(o.value, 3) + '</td><td>' + WW.escape(o.unit) + '</td><td>' + WW.status(o.qualityFlag) + '</td><td>' + WW.classification(o.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No observations', 'Sync Open-Meteo rainfall or import gauge observations.', 'fa-area-chart');
      if (!data.length) { document.getElementById('hyd-trend-wrap').innerHTML = WW.empty('No observations', 'No chart can be drawn without observations.', 'fa-area-chart'); return; }
      document.getElementById('hyd-trend-wrap').innerHTML = '<canvas id="hyd-trend-chart"></canvas>';
      WW.chart('hyd-trend-chart', {
        type: 'line',
        data: { labels: data.slice().reverse().map(o => WW.fmtDate(o.observedAt)), datasets: [WW.lineDataset(parameter, data.slice().reverse().map(o => Number(o.value)), '#0369a1', true)] },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: WW.axis(''), y: WW.axis(data[0].unit) } }
      });
    } catch (e) { WW.toast(e.message, 'error', 'Observations'); }
  }

  function renderObstructions(items) {
    document.getElementById('obs-count').textContent = items.length + ' reports';
    document.getElementById('obstruction-list').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Reference</th><th>Waterway</th><th>Severity</th><th>Status</th><th>Reported</th></tr></thead><tbody>' +
        items.map(o => '<tr><td>' + WW.escape(o.reference) + '</td><td>' + WW.escape(o.waterwayName || '—') + '</td><td>' + WW.status(o.severity) + '</td><td>' + WW.escape(o.status) + '</td><td>' + WW.fmtDate(o.reportedAt) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No obstruction reports', 'Reported drainage obstructions will appear here.', 'fa-check');
  }

  async function syncRainfall() {
    try {
      const result = await WW.post('/api/hydrology/rainfall/sync?days=30', {});
      WW.toast('Open-Meteo rainfall sync complete: ' + result.imported + ' new observations. Gridded reanalysis, not a rain gauge.', 'warn', 'Rainfall');
      await refresh();
    } catch (e) { WW.toast(e.message, 'error', 'Rainfall sync'); }
  }
})();
