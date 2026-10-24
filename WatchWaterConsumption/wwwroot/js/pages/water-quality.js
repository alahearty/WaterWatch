(function () {
  'use strict';
  const canManage = document.getElementById('quality-permissions')?.dataset.canManage === 'true';
  let stations = [];
  let campaigns = [];
  let standards = [];
  let map;
  let stationLayer;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    initMap();
    bind();
    await loadReference();
    await refreshAll();
  }

  function initMap() {
    map = L.map('quality-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
    WW.get('/api/layers/admin-lga/geojson').then(data => {
      L.geoJSON(data, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.02 } }).addTo(map);
      const bounds = L.geoJSON(data).getBounds();
      if (bounds.isValid()) map.fitBounds(bounds, { padding: [18, 18] });
    }).catch(() => { });
    stationLayer = L.layerGroup().addTo(map);
  }

  function bind() {
    document.getElementById('new-station')?.addEventListener('click', () => WW.modal.open('station-modal'));
    document.getElementById('new-campaign')?.addEventListener('click', () => WW.modal.open('campaign-modal'));
    document.getElementById('new-sample')?.addEventListener('click', () => WW.modal.open('sample-modal'));
    document.getElementById('trend-station').addEventListener('change', loadTrend);
    document.getElementById('trend-parameter').addEventListener('change', loadTrend);
    document.getElementById('station-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/water-quality/stations', {
          code: data.get('code'), name: data.get('name'), waterSourceType: data.get('waterSourceType'),
          latitude: Number(data.get('latitude')), longitude: Number(data.get('longitude')),
          description: data.get('description') || null
        });
        WW.modal.close('station-modal'); WW.toast('Sampling station created.', 'warn', 'Saved'); await loadReference(); await refreshAll();
      } catch (ex) { WW.toast(ex.message, 'error', 'Station'); }
    });
    document.getElementById('campaign-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/water-quality/campaigns', {
          code: data.get('code'), name: data.get('name'), startedOn: data.get('startedOn'),
          endedOn: data.get('endedOn') || null, laboratoryName: data.get('laboratoryName') || null,
          laboratoryAccredited: data.get('laboratoryAccredited') === 'on',
          accreditationReference: data.get('accreditationReference') || null, purpose: data.get('purpose') || null
        });
        WW.modal.close('campaign-modal'); WW.toast('Campaign created.', 'warn', 'Saved'); await loadReference(); await refreshAll();
      } catch (ex) { WW.toast(ex.message, 'error', 'Campaign'); }
    });
    document.getElementById('sample-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/water-quality/samples', {
          sampleCode: data.get('sampleCode'), stationId: data.get('stationId'), campaignId: data.get('campaignId'),
          sampledAt: new Date(data.get('sampledAt')).toISOString(), sampledBy: data.get('sampledBy') || null,
          notes: data.get('notes') || null, results: []
        });
        WW.modal.close('sample-modal'); WW.toast('Sample created. Add results next.', 'warn', 'Saved'); await refreshAll();
      } catch (ex) { WW.toast(ex.message, 'error', 'Sample'); }
    });
  }

  async function loadReference() {
    try {
      const [s, c, st] = await Promise.all([
        WW.get('/api/water-quality/stations'),
        WW.get('/api/water-quality/campaigns'),
        WW.get('/api/water-quality/standards')
      ]);
      stations = s; campaigns = c; standards = st;
      document.getElementById('trend-station').innerHTML = stations.map(x => '<option value="' + x.id + '">' + WW.escape(x.name) + '</option>').join('');
      const parameters = ['pH', 'TemperatureC', 'TurbidityNTU', 'ElectricalConductivityUScm', 'TotalDissolvedSolidsMgL', 'DissolvedOxygenMgL', 'BOD5MgL', 'CODMgL', 'NitrateMgL', 'ChlorideMgL', 'IronMgL', 'LeadMgL', 'TotalColiformCfu100ml', 'EColiCfu100ml', 'HydrocarbonMgL'];
      document.getElementById('trend-parameter').innerHTML = parameters.map(p => '<option>' + p + '</option>').join('');
      const stationOptions = stations.map(x => '<option value="' + x.id + '">' + WW.escape(x.name) + '</option>').join('');
      document.getElementById('sample-station').innerHTML = stationOptions;
      document.getElementById('sample-campaign').innerHTML = campaigns.map(x => '<option value="' + x.id + '">' + WW.escape(x.name) + '</option>').join('');
      document.getElementById('quality-standards').innerHTML = standards.map(s => '<div class="mb-2"><div class="flex-between"><strong>' + WW.escape(s.name) + '</strong><span class="badge badge-outline">' + WW.escape(s.code) + '</span></div><div class="tiny muted mb-1">' + WW.escape(s.authority) + ' · ' + WW.escape(s.version) + '</div><div class="table-wrap"><table class="ww-table"><thead><tr><th>Parameter</th><th>Unit</th><th>Min</th><th>Max</th><th>Note</th></tr></thead><tbody>' +
        s.limits.map(l => '<tr><td>' + WW.escape(l.parameter) + '</td><td>' + WW.escape(l.unit) + '</td><td>' + (l.minValue ?? '—') + '</td><td>' + (l.maxValue ?? '—') + '</td><td class="small">' + WW.escape(l.notes || '') + '</td></tr>').join('') + '</tbody></table></div></div>').join('');
      document.getElementById('quality-standard-note').textContent = standards.length ? 'Limits are sourced from ' + standards.map(s => s.code).join(' and ') + '. Review the configured standard before regulatory use.' : 'No standards configured.';
      if (stations.length && document.getElementById('trend-station').value) loadTrend();
    } catch (e) { WW.toast(e.message, 'error', 'Quality reference'); }
  }

  async function refreshAll() {
    const [summary, samples, exceedances, hotspots, choropleth] = await Promise.all([
      WW.get('/api/water-quality/summary'),
      WW.get('/api/water-quality/samples?page=1&pageSize=100'),
      WW.get('/api/water-quality/exceedances'),
      WW.get('/api/water-quality/hotspots'),
      WW.get('/api/water-quality/choropleth')
    ]);
    document.getElementById('q-stations').textContent = WW.fmtInt(summary.stations);
    document.getElementById('q-campaigns').textContent = summary.campaigns + ' campaigns';
    document.getElementById('q-samples').textContent = WW.fmtInt(summary.samples);
    document.getElementById('q-last').textContent = summary.lastSampleAt ? 'Last ' + WW.fmtRelative(summary.lastSampleAt) : 'No samples';
    document.getElementById('q-noncompliant').textContent = WW.fmtInt(summary.nonCompliant);
    document.getElementById('q-insufficient').textContent = WW.fmtInt(summary.insufficientEvidence);
    document.getElementById('quality-availability').innerHTML = '<div class="alert alert-' + (summary.samples ? 'info' : 'warn') + '"><i class="fa fa-info-circle"></i><div>' + WW.escape(summary.availabilityMessage) + '</div></div>';
    renderChart(summary);
    renderSamples(samples.items);
    renderExceedances(exceedances);
    renderHotspots(hotspots);
    renderMap(choropleth);
  }

  function renderChart(summary) {
    if (!summary.results) { document.getElementById('quality-state-chart').parentElement.innerHTML = WW.empty('Not assessed', 'No sample results are recorded.', 'fa-flask'); return; }
    WW.chart('quality-state-chart', {
      type: 'bar',
      data: {
        labels: ['Compliant', 'Non-compliant', 'Insufficient evidence', 'Not assessed'],
        datasets: [{ data: [summary.compliant, summary.nonCompliant, summary.insufficientEvidence, summary.notAssessed], backgroundColor: ['#1f9d67', '#c0392b', '#b7791f', '#8a9aab'], borderRadius: 5 }]
      },
      options: { responsive: true, maintainAspectRatio: false, indexAxis: 'y', plugins: { legend: { display: false } }, scales: { x: { ...WW.axis('results'), ticks: { precision: 0 } }, y: WW.axis('') } }
    });
  }

  function renderSamples(items) {
    document.getElementById('quality-samples').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Sample</th><th>Station</th><th>Sampled</th><th>Class</th></tr></thead><tbody>' +
        items.slice(0, 15).map(s => '<tr class="clickable" data-sample="' + s.id + '"><td><strong>' + WW.escape(s.sampleCode) + '</strong></td><td>' + WW.escape(s.stationName) + '</td><td>' + WW.fmtDate(s.sampledAt) + '</td><td>' + WW.classification(s.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No samples', 'Create a sampling campaign and record a sample.', 'fa-flask');
    document.querySelectorAll('[data-sample]').forEach(row => row.addEventListener('click', () => openSample(row.dataset.sample)));
  }

  function renderExceedances(items) {
    document.getElementById('quality-exceedances').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Station</th><th>Parameter</th><th>Value</th><th>Limit</th><th>Sampled</th></tr></thead><tbody>' +
        items.slice(0, 15).map(e => '<tr><td>' + WW.escape(e.stationName) + '<div class="tiny muted">' + WW.escape(e.sampleCode) + '</div></td><td>' + WW.escape(e.parameter) + '</td><td>' + WW.escape(e.value ?? '—') + ' ' + WW.escape(e.unit) + '</td><td class="small">' + (e.limitMin != null ? '≥' + e.limitMin : '') + (e.limitMin != null && e.limitMax != null ? ' / ' : '') + (e.limitMax != null ? '≤' + e.limitMax : '') + '</td><td>' + WW.fmtDate(e.sampledAt) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No exceedances', 'No non-compliant results are recorded, or no standards have been applied.', 'fa-check');
  }

  function renderHotspots(items) {
    document.getElementById('quality-hotspots').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Station</th><th>Parameter</th><th class="num">Non-compliant</th><th>Latest state</th></tr></thead><tbody>' +
        items.slice(0, 20).map(h => '<tr><td><a href="/WaterQuality/Station/' + h.stationId + '">' + WW.escape(h.stationName) + '</a></td><td>' + WW.escape(h.parameter) + '</td><td class="num">' + h.nonCompliantResults + ' / ' + h.totalResults + '</td><td>' + WW.stateBadge(h.latestState) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No hotspots', 'Hotspots require non-compliant sample results.', 'fa-map-marker');
  }

  function renderMap(geojson) {
    stationLayer.clearLayers();
    L.geoJSON(geojson, {
      pointToLayer: (f, latlng) => {
        const color = f.properties.latestState === 'NonCompliant' ? '#c0392b' : f.properties.latestState === 'Compliant' ? '#1f9d67' : f.properties.latestState === 'InsufficientEvidence' ? '#b7791f' : '#8a9aab';
        return L.circleMarker(latlng, { radius: 7, color: '#fff', weight: 2, fillColor: color, fillOpacity: 0.95 });
      },
      onEachFeature: (f, layer) => layer.bindPopup('<div class="popup-title">' + WW.escape(f.properties.name) + '</div><div class="popup-row"><span>Latest state</span><span>' + WW.escape(f.properties.latestState) + '</span></div><div class="popup-row"><span>Last sample</span><span>' + WW.fmtDate(f.properties.lastSampledAt) + '</span></div><div class="popup-row"><span>Non-compliant results</span><span>' + f.properties.nonCompliantCount + '</span></div>')
    }).addTo(stationLayer);
  }

  async function loadTrend() {
    const stationId = document.getElementById('trend-station').value;
    const parameter = document.getElementById('trend-parameter').value;
    if (!stationId) return;
    try {
      const data = await WW.get('/api/water-quality/stations/' + stationId + '/trend?parameter=' + encodeURIComponent(parameter));
      if (!data.length) {
        document.getElementById('quality-trend-wrap').innerHTML = WW.empty('No trend', 'No results for this station and parameter.', 'fa-line-chart');
        return;
      }
      document.getElementById('quality-trend-wrap').innerHTML = '<canvas id="quality-trend-chart"></canvas>';
      const limit = data.find(d => d.limitMax != null);
      WW.chart('quality-trend-chart', {
        type: 'line',
        data: {
          labels: data.map(d => d.period),
          datasets: [
            WW.lineDataset(parameter + ' (' + (data[0].unit || '') + ')', data.map(d => d.value), '#0f8b8d', true),
            { label: 'Limit', data: data.map(() => limit ? Number(limit.limitMax) : null), borderColor: '#c0392b', borderDash: [5, 4], borderWidth: 1.5, pointRadius: 0, fill: false }
          ]
        },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { x: WW.axis(''), y: WW.axis(data[0].unit || '') } }
      });
    } catch (e) { WW.toast(e.message, 'error', 'Trend'); }
  }

  async function openSample(id) {
    try {
      const sample = await WW.get('/api/water-quality/samples/' + id);
      const rows = sample.results.map(r => '<tr><td>' + WW.escape(r.parameter) + '</td><td class="num">' + (r.value ?? '—') + '</td><td>' + WW.escape(r.unit) + '</td><td>' + WW.stateBadge(r.state) + '</td><td class="small">' + WW.escape(r.standardCode || '—') + '<div class="tiny muted">' + WW.escape(r.assessmentNote || '') + '</div></td></tr>').join('');
      document.getElementById('sample-detail-body').innerHTML =
        '<div class="flex-between flex-wrap mb-2"><div><div class="inspector-title">' + WW.escape(sample.sampleCode) + '</div><div class="inspector-sub">' + WW.escape(sample.stationName) + ' · ' + WW.fmtDate(sample.sampledAt, true) + ' · ' + WW.classification(sample.dataClassification) + '</div></div>' +
        (canManage ? '<button class="btn btn-primary btn-sm" id="add-result-button" type="button"><i class="fa fa-plus"></i> Add result</button>' : '') + '</div>' +
        '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Parameter</th><th class="num">Value</th><th>Unit</th><th>State</th><th>Standard / note</th></tr></thead><tbody>' + (rows || '<tr><td colspan="5" class="muted">No results entered.</td></tr>') + '</tbody></table></div>' +
        '<div id="add-result-form-wrap" class="mt-2"></div>';
      document.getElementById('add-result-button')?.addEventListener('click', () => renderAddResult(sample));
      WW.modal.open('sample-detail-modal');
    } catch (e) { WW.toast(e.message, 'error', 'Sample'); }
  }

  function renderAddResult(sample) {
    const standard = standards.find(s => s.code === 'NSDWQ-2007') || standards[0];
    if (!standard) { WW.toast('No quality standard configured.', 'warn'); return; }
    const wrap = document.getElementById('add-result-form-wrap');
    wrap.innerHTML = '<div class="card"><div class="card-head"><h3>Add result</h3><span class="badge badge-outline">' + WW.escape(standard.code) + '</span></div><div class="card-body">' +
      '<div class="field-row"><div class="field"><label>Parameter</label><select class="select" id="result-param">' + standard.limits.map(l => '<option>' + WW.escape(l.parameter) + '</option>').join('') + '</select></div>' +
      '<div class="field"><label>Value</label><input class="input" id="result-value" type="number" step="0.0001" /></div></div>' +
      '<div class="field-row"><div class="field"><label>Unit</label><input class="input" id="result-unit" readonly /></div><div class="field"><label>Detection limit</label><input class="input" id="result-detect" type="number" step="0.0001" /></div></div>' +
      '<div class="field"><label>Method / note</label><input class="input" id="result-method" /></div>' +
      '<button class="btn btn-primary" id="save-result" type="button">Save result</button></div></div>';
    const param = document.getElementById('result-param');
    const unit = document.getElementById('result-unit');
    const syncUnit = () => { const limit = standard.limits.find(l => l.parameter === param.value); unit.value = limit ? limit.unit : ''; };
    param.addEventListener('change', syncUnit); syncUnit();
    document.getElementById('save-result').addEventListener('click', async () => {
      try {
        await WW.post('/api/water-quality/samples/' + sample.id + '/results', {
          parameter: param.value, value: document.getElementById('result-value').value === '' ? null : Number(document.getElementById('result-value').value),
          unit: unit.value, detectionLimit: document.getElementById('result-detect').value ? Number(document.getElementById('result-detect').value) : null,
          method: document.getElementById('result-method').value || null, standardCode: standard.code
        });
        WW.toast('Result added and classified.', 'warn', 'Saved');
        await openSample(sample.id); await refreshAll();
      } catch (e) { WW.toast(e.message, 'error', 'Result'); }
    });
  }
})();
