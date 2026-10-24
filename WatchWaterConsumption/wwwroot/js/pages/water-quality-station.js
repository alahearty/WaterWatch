(function () {
  'use strict';
  const root = document.getElementById('station-root');
  const stationId = root.dataset.stationId;
  let station = null;
  const map = L.map('station-map', { zoomControl: true }).setView([4.8156, 7.0498], 13);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  const layers = L.layerGroup().addTo(map);

  async function init() {
    try {
      const stations = await WW.get('/api/water-quality/stations');
      station = stations.find(s => s.id === stationId);
      if (!station) throw new Error('Station not found.');
      renderStation();
      bind();
      await Promise.all([loadSamples(), loadHotspots(), loadTrend()]);
    } catch (e) {
      document.getElementById('station-name').textContent = 'Station unavailable';
      document.getElementById('station-subtitle').textContent = e.message;
    }
  }

  function renderStation() {
    document.title = station.name + ' · WaterWatch';
    document.getElementById('station-name').textContent = station.name;
    document.getElementById('station-subtitle').innerHTML = WW.escape(station.code) + ' · ' + WW.escape(station.waterSourceType) + ' · ' + WW.escape(station.adminArea || 'No LGA') + ' ' + (station.isActive ? '<span class="badge badge-green">Active</span>' : '<span class="badge badge-grey">Inactive</span>');
    document.getElementById('station-class').innerHTML = WW.classification(station.dataClassification);
    document.getElementById('station-facts').innerHTML =
      '<div class="kv"><dt>Code</dt><dd>' + WW.escape(station.code) + '</dd></div>' +
      '<div class="kv"><dt>Source type</dt><dd>' + WW.escape(station.waterSourceType) + '</dd></div>' +
      '<div class="kv"><dt>Admin area</dt><dd>' + WW.escape(station.adminArea || '—') + '</dd></div>' +
      '<div class="kv"><dt>Service zone</dt><dd>' + WW.escape(station.serviceZone || '—') + '</dd></div>' +
      '<div class="kv"><dt>Coordinates</dt><dd>' + (station.latitude != null ? station.latitude.toFixed(6) + ', ' + station.longitude.toFixed(6) : '—') + '</dd></div>' +
      '<div class="kv"><dt>Last sample</dt><dd>' + WW.fmtDate(station.lastSampledAt, true) + '</dd></div>' +
      '<div class="kv"><dt>Samples</dt><dd>' + station.sampleCount + '</dd></div>' +
      (station.latitude != null ? '<div class="alert alert-info mt-2"><i class="fa fa-info-circle"></i><div>A station coordinate is not a compliance boundary. Results apply to the sample and time recorded.</div></div>' : '');
    if (station.latitude != null) {
      L.circleMarker([station.latitude, station.longitude], { radius: 8, color: '#fff', weight: 2, fillColor: '#be123c', fillOpacity: 0.9 }).addTo(layers).bindPopup(WW.escape(station.name));
      map.setView([station.latitude, station.longitude], 15);
    }
    WW.get('/api/layers/admin-lga/geojson').then(data => { L.geoJSON(data, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0 } }).addTo(map); }).catch(() => { });
  }

  function bind() {
    document.getElementById('station-parameter').innerHTML = ['pH', 'TurbidityNTU', 'TotalDissolvedSolidsMgL', 'NitrateMgL', 'ChlorideMgL', 'IronMgL', 'LeadMgL', 'EColiCfu100ml', 'TotalColiformCfu100ml', 'ElectricalConductivityUScm'].map(p => '<option>' + p + '</option>').join('');
    document.getElementById('station-parameter').addEventListener('change', loadTrend);
  }

  async function loadSamples() {
    try {
      const data = await WW.get('/api/water-quality/samples?stationId=' + stationId + '&page=1&pageSize=100');
      document.getElementById('station-sample-count').textContent = data.total + ' samples';
      document.getElementById('station-samples').innerHTML = data.items.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Sample</th><th>Sampled</th><th>Campaign</th><th>Results</th><th>Class</th></tr></thead><tbody>' +
          data.items.map(s => '<tr><td><strong>' + WW.escape(s.sampleCode) + '</strong></td><td>' + WW.fmtDate(s.sampledAt, true) + '</td><td>' + WW.escape(s.campaignCode) + '</td><td>' + s.results.length + '</td><td>' + WW.classification(s.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No samples', 'No samples are recorded at this station.', 'fa-flask');
    } catch (e) { WW.toast(e.message, 'error', 'Samples'); }
  }

  async function loadHotspots() {
    try {
      const all = await WW.get('/api/water-quality/hotspots');
      const rows = all.filter(h => h.stationId === stationId);
      document.getElementById('station-hotspots').innerHTML = rows.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Parameter</th><th class="num">Non-compliant</th><th>Latest</th></tr></thead><tbody>' +
          rows.map(h => '<tr><td>' + WW.escape(h.parameter) + '</td><td class="num">' + h.nonCompliantResults + ' / ' + h.totalResults + '</td><td>' + WW.stateBadge(h.latestState) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No hotspots', 'No non-compliant results recorded at this station.', 'fa-check');
    } catch (e) { WW.toast(e.message, 'error', 'Hotspots'); }
  }

  async function loadTrend() {
    try {
      const parameter = document.getElementById('station-parameter').value;
      const data = await WW.get('/api/water-quality/stations/' + stationId + '/trend?parameter=' + encodeURIComponent(parameter));
      if (!data.length) { document.getElementById('station-trend-wrap').innerHTML = WW.empty('No trend', 'No results for this parameter.', 'fa-line-chart'); return; }
      document.getElementById('station-trend-wrap').innerHTML = '<canvas id="station-trend-chart"></canvas>';
      const limit = data.find(d => d.limitMax != null);
      WW.chart('station-trend-chart', {
        type: 'line',
        data: { labels: data.map(d => d.period), datasets: [
          WW.lineDataset(parameter, data.map(d => d.value), '#0f8b8d', true),
          { label: 'Limit', data: data.map(() => limit ? Number(limit.limitMax) : null), borderColor: '#c0392b', borderDash: [5, 4], borderWidth: 1.5, pointRadius: 0, fill: false }
        ] },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { x: WW.axis(''), y: WW.axis(data[0].unit || '') } }
      });
    } catch (e) { WW.toast(e.message, 'error', 'Trend'); }
  }

  init();
})();
