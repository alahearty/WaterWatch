(function () {
  'use strict';
  const canManage = document.getElementById('groundwater-permissions')?.dataset.canManage === 'true';
  let boreholes = [];
  let selected = null;
  let selectedDetail = null;
  const map = L.map('groundwater-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  const markerLayer = L.layerGroup().addTo(map);

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    await loadBaseMap();
    await refresh();
    bind();
  }

  function bind() {
    document.getElementById('gw-radius-apply').addEventListener('click', () => selected && loadContamination(selected.assetId));
    document.getElementById('gw-detail-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.put('/api/groundwater/boreholes/' + selected.assetId + '/details', {
          boreholeDepthM: num(data.get('boreholeDepthM')), yieldM3PerHour: num(data.get('yieldM3PerHour')),
          screenTopM: num(data.get('screenTopM')), screenBottomM: num(data.get('screenBottomM')),
          staticWaterLevelM: num(data.get('staticWaterLevelM')), dynamicWaterLevelM: num(data.get('dynamicWaterLevelM')),
          aquiferName: data.get('aquiferName') || null, aquiferClassification: data.get('aquiferClassification') || null,
          aquiferClassificationInferred: data.get('aquiferClassificationInferred') === 'on',
          drilledOn: data.get('drilledOn') || null, driller: data.get('driller') || null,
          constructionJson: data.get('constructionJson') || null, lithologyJson: data.get('lithologyJson') || null
        });
        WW.modal.close('gw-detail-modal'); WW.toast('Borehole details saved.', 'warn', 'Saved'); await select(selected.assetId);
      } catch (ex) { WW.toast(ex.message, 'error', 'Borehole details'); }
    });
    document.getElementById('gw-reading-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/groundwater/boreholes/' + selected.assetId + '/readings', {
          observedAt: new Date(data.get('observedAt')).toISOString(), waterLevelM: num(data.get('waterLevelM')),
          abstractionM3: num(data.get('abstractionM3')), notes: data.get('notes') || null
        });
        WW.modal.close('gw-reading-modal'); WW.toast('Groundwater reading saved.', 'warn', 'Saved'); await select(selected.assetId); await loadSummary();
      } catch (ex) { WW.toast(ex.message, 'error', 'Groundwater reading'); }
    });
    document.getElementById('gw-test-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/groundwater/boreholes/' + selected.assetId + '/pumping-tests', {
          testedAt: new Date(data.get('testedAt')).toISOString(), durationMinutes: Number(data.get('durationMinutes')),
          pumpingRateM3PerHour: num(data.get('pumpingRateM3PerHour')), drawdownM: num(data.get('drawdownM')),
          recoveryMinutes: data.get('recoveryMinutes') ? Number(data.get('recoveryMinutes')) : null,
          conductedBy: data.get('conductedBy') || null, resultJson: data.get('resultJson') || null
        });
        WW.modal.close('gw-test-modal'); WW.toast('Pumping test saved.', 'warn', 'Saved'); await select(selected.assetId);
      } catch (ex) { WW.toast(ex.message, 'error', 'Pumping test'); }
    });
  }

  function num(value) { return value === null || value === undefined || value === '' ? null : Number(value); }

  async function loadBaseMap() {
    try {
      const [ctx, admin] = await Promise.all([WW.get('/api/geography/context'), WW.get('/api/layers/admin-lga/geojson')]);
      L.geoJSON(admin, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.02 } }).addTo(map);
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [18, 18] });
    } catch { }
  }

  async function loadSummary() {
    try {
      const s = await WW.get('/api/groundwater/summary');
      document.getElementById('g-boreholes').textContent = WW.fmtInt(s.boreholes);
      document.getElementById('g-details').textContent = s.withDetails + ' with construction details';
      document.getElementById('g-readings').textContent = WW.fmtInt(s.readings);
      document.getElementById('g-last').textContent = s.lastReadingAt ? 'Last ' + WW.fmtRelative(s.lastReadingAt) : 'No readings';
      document.getElementById('g-tests').textContent = WW.fmtInt(s.pumpingTests);
      document.getElementById('g-inferred').textContent = WW.fmtInt(s.aquiferInferred);
      document.getElementById('groundwater-availability').innerHTML = '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>' + WW.escape(s.availabilityMessage) + '</div></div>';
    } catch { }
  }

  async function refresh() {
    await loadSummary();
    try {
      const data = await WW.get('/api/groundwater/boreholes');
      boreholes = data;
      document.getElementById('g-count').textContent = data.length + ' registered';
      document.getElementById('borehole-table').innerHTML = data.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Borehole</th><th>Depth</th><th>Aquifer</th><th class="num">SWL</th><th class="num">Yield</th><th>Last reading</th><th>Status</th></tr></thead><tbody>' +
          data.map(b => '<tr class="clickable" data-borehole="' + b.assetId + '"><td><strong>' + WW.escape(b.name) + '</strong><div class="tiny muted">' + WW.escape(b.assetCode) + '</div></td><td>' + (b.depthM ? WW.fmt(b.depthM, 1) + ' m' : '—') + '</td><td class="small">' + WW.escape(b.aquiferName || '—') + (b.aquiferClassificationInferred ? '<div class="tiny muted">inferred</div>' : '') + '</td><td class="num">' + (b.staticWaterLevelM != null ? WW.fmt(b.staticWaterLevelM, 2) + ' m' : '—') + '</td><td class="num">' + (b.yieldM3PerHour != null ? WW.fmt(b.yieldM3PerHour, 2) + ' m³/h' : '—') + '</td><td>' + WW.fmtDate(b.lastReadingAt) + '</td><td>' + WW.status(b.status) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No boreholes', 'Register a borehole or well asset in Infrastructure, then add construction details.', 'fa-arrow-down');
      document.querySelectorAll('[data-borehole]').forEach(row => row.addEventListener('click', () => select(row.dataset.borehole)));
      markerLayer.clearLayers();
      data.forEach(b => {
        if (b.longitude != null) {
          L.circleMarker([b.latitude, b.longitude], { radius: 6, color: '#fff', weight: 2, fillColor: '#0369a1', fillOpacity: 0.9 }).addTo(markerLayer)
            .bindPopup('<div class="popup-title">' + WW.escape(b.name) + '</div><div class="popup-row"><span>Aquifer</span><span>' + WW.escape(b.aquiferName || '—') + '</span></div>');
        }
      });
      if (data.length) await select(data[0].assetId);
    } catch (e) { document.getElementById('borehole-table').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  async function select(assetId) {
    try {
      const [detail, readings, tests, trend] = await Promise.all([
        WW.get('/api/groundwater/boreholes/' + assetId),
        WW.get('/api/groundwater/boreholes/' + assetId + '/readings'),
        WW.get('/api/groundwater/boreholes/' + assetId + '/pumping-tests'),
        WW.get('/api/groundwater/boreholes/' + assetId + '/trend')
      ]);
      selected = boreholes.find(b => b.assetId === assetId) || { assetId };
      selectedDetail = Object.assign({}, selected, detail);
      document.getElementById('selected-class').innerHTML = WW.classification(detail.dataClassification);
      document.getElementById('borehole-detail').innerHTML =
        '<div class="inspector-title">' + WW.escape(detail.name) + '</div><div class="inspector-sub">' + WW.escape(detail.assetCode) + ' · ' + WW.status(detail.status) + ' · ' + WW.status(detail.condition) + '</div>' +
        '<div class="flex gap-1 flex-wrap mb-2">' +
        (canManage ? '<button class="btn btn-sm btn-primary" id="edit-gw-details"><i class="fa fa-pencil"></i> Construction</button><button class="btn btn-sm" id="add-gw-reading"><i class="fa fa-plus"></i> Reading</button><button class="btn btn-sm" id="add-gw-test"><i class="fa fa-flask"></i> Pumping test</button>' : '') +
        '</div>' +
        '<dl class="kv"><dt>Depth</dt><dd>' + (detail.boreholeDepthM ? WW.fmt(detail.boreholeDepthM, 2) + ' m' : '—') + '</dd>' +
        '<dt>Screen</dt><dd>' + (detail.screenTopM != null ? detail.screenTopM + ' – ' + detail.screenBottomM + ' m' : '—') + '</dd>' +
        '<dt>Aquifer</dt><dd>' + WW.escape(detail.aquiferName || '—') + (detail.aquiferClassificationInferred ? ' <span class="badge badge-amber">inferred</span>' : ' <span class="badge badge-green">observed</span>') + '</dd>' +
        '<dt>Static level</dt><dd>' + (detail.staticWaterLevelM != null ? detail.staticWaterLevelM + ' m' : '—') + '</dd>' +
        '<dt>Dynamic level</dt><dd>' + (detail.dynamicWaterLevelM != null ? detail.dynamicWaterLevelM + ' m' : '—') + '</dd>' +
        '<dt>Yield</dt><dd>' + (detail.yieldM3PerHour != null ? detail.yieldM3PerHour + ' m³/h' : '—') + '</dd>' +
        '<dt>Drilled</dt><dd>' + WW.escape(detail.drilledOn || '—') + '</dd><dt>Driller</dt><dd>' + WW.escape(detail.driller || '—') + '</dd></dl>' +
        '<div class="availability mt-2"><i class="fa fa-book"></i><div>Observed water levels are separate from inferred aquifer classifications. Nearby contamination uses geometric distance to sampling stations.</div></div>';
      document.getElementById('edit-gw-details')?.addEventListener('click', openDetailModal);
      document.getElementById('add-gw-reading')?.addEventListener('click', () => { document.getElementById('gw-reading-form').reset(); setNow('gw-reading-form', 'observedAt'); WW.modal.open('gw-reading-modal'); });
      document.getElementById('add-gw-test')?.addEventListener('click', () => { document.getElementById('gw-test-form').reset(); setNow('gw-test-form', 'testedAt'); WW.modal.open('gw-test-modal'); });
      renderTrend(trend);
      renderTests(tests);
      await loadContamination(assetId);
      map.setView([selected.latitude, selected.longitude], 14);
    } catch (e) { WW.toast(e.message, 'error', 'Borehole'); }
  }

  function renderTrend(trend) {
    const wrap = document.getElementById('gw-trend-wrap');
    if (!trend.length) { wrap.innerHTML = WW.empty('No readings', 'Add groundwater readings to draw a trend.', 'fa-line-chart'); return; }
    wrap.innerHTML = '<canvas id="gw-trend-chart"></canvas>';
    WW.chart('gw-trend-chart', {
      type: 'line',
      data: { labels: trend.map(t => t.period), datasets: [WW.lineDataset('Average water level (m)', trend.map(t => t.averageWaterLevelM), '#0369a1', true)] },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: WW.axis(''), y: WW.axis('m') } }
    });
  }

  function renderTests(tests) {
    document.getElementById('gw-tests-list').innerHTML = tests.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Tested</th><th class="num">Duration</th><th class="num">Pumping rate</th><th class="num">Drawdown</th><th>Conducted by</th><th>Class</th></tr></thead><tbody>' +
        tests.map(t => '<tr><td>' + WW.fmtDate(t.testedAt) + '</td><td class="num">' + t.durationMinutes + ' min</td><td class="num">' + (t.pumpingRateM3PerHour ?? '—') + ' m³/h</td><td class="num">' + (t.drawdownM ?? '—') + ' m</td><td>' + WW.escape(t.conductedBy || '—') + '</td><td>' + WW.classification(t.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No pumping tests', 'Record a pumping test to evidence yield and drawdown.', 'fa-flask');
  }

  async function loadContamination(assetId) {
    const radius = Number(document.getElementById('gw-radius').value) || 1000;
    try {
      const data = await WW.get('/api/groundwater/boreholes/' + assetId + '/contamination-proximity?radiusMeters=' + radius);
      document.getElementById('gw-contamination').innerHTML = data.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Station</th><th class="num">Distance</th><th class="num">Non-compliant</th><th>Parameters</th></tr></thead><tbody>' +
          data.map(c => '<tr><td><a href="/WaterQuality/Station/' + c.stationId + '">' + WW.escape(c.stationName) + '</a></td><td class="num">' + WW.fmt(c.distanceM, 1) + ' m</td><td class="num">' + c.nonCompliantParameters + '</td><td class="small">' + WW.escape(c.parameters.join(', ') || '—') + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No nearby stations', 'No sampling station with a latest sample is within ' + radius + ' m.', 'fa-check');
    } catch (e) { document.getElementById('gw-contamination').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  function openDetailModal() {
    const form = document.getElementById('gw-detail-form');
    const d = selectedDetail;
    form.querySelector('[name=boreholeDepthM]').value = d.boreholeDepthM ?? '';
    form.querySelector('[name=yieldM3PerHour]').value = d.yieldM3PerHour ?? '';
    form.querySelector('[name=screenTopM]').value = d.screenTopM ?? '';
    form.querySelector('[name=screenBottomM]').value = d.screenBottomM ?? '';
    form.querySelector('[name=staticWaterLevelM]').value = d.staticWaterLevelM ?? '';
    form.querySelector('[name=dynamicWaterLevelM]').value = d.dynamicWaterLevelM ?? '';
    form.querySelector('[name=aquiferName]').value = d.aquiferName || '';
    form.querySelector('[name=aquiferClassification]').value = d.aquiferClassification || '';
    form.querySelector('[name=aquiferClassificationInferred]').checked = d.aquiferClassificationInferred !== false;
    form.querySelector('[name=drilledOn]').value = d.drilledOn || '';
    form.querySelector('[name=driller]').value = d.driller || '';
    form.querySelector('[name=constructionJson]').value = d.constructionJson || '';
    form.querySelector('[name=lithologyJson]').value = d.lithologyJson || '';
    WW.modal.open('gw-detail-modal');
  }

  function setNow(formId, field) {
    const input = document.querySelector('#' + formId + ' [name=' + field + ']');
    input.value = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  }
})();
