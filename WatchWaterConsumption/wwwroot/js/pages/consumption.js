(function () {
  'use strict';
  const canImport = !!document.getElementById('open-import');
  const canCorrect = document.getElementById('consumption-permissions')?.dataset.canCorrect === 'true';
  let dimension = 'lga';
  let registryTab = 'accounts';
  let trendMonths = [];
  let playbackTimer = null;
  let registryTimer = null;
  let map;
  let choroplethLayer;
  let legendControl;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    initMap();
    bindControls();
    await refreshAll();
  }

  function initMap() {
    map = L.map('consumption-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
    choroplethLayer = L.geoJSON(null, { style: styleFor }).addTo(map);
    WW.get('/api/geography/context').then(ctx => {
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [18, 18] });
    }).catch(() => { });
  }

  function bindControls() {
    document.querySelectorAll('#consumption-dimension button').forEach(btn => btn.addEventListener('click', () => {
      document.querySelectorAll('#consumption-dimension button').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      dimension = btn.dataset.dim;
      document.getElementById('choropleth-dim').textContent = btn.textContent;
      refreshAll();
    }));
    document.getElementById('cons-apply').addEventListener('click', refreshAll);
    document.getElementById('cons-playback-month').addEventListener('change', () => applyPlaybackMonth());
    document.getElementById('cons-play').addEventListener('click', togglePlayback);
    document.getElementById('cons-reset').addEventListener('click', () => {
      document.getElementById('cons-from').value = '';
      document.getElementById('cons-to').value = '';
      refreshAll();
    });
    document.getElementById('open-import')?.addEventListener('click', () => WW.modal.open('import-modal'));
    document.getElementById('run-import')?.addEventListener('click', runImport);
    document.querySelectorAll('#registry-tabs button').forEach(btn => btn.addEventListener('click', () => {
      document.querySelectorAll('#registry-tabs button').forEach(b => b.classList.remove('active'));
      btn.classList.add('active'); registryTab = btn.dataset.reg; loadRegistry();
    }));
    document.getElementById('registry-search').addEventListener('input', () => {
      clearTimeout(registryTimer); registryTimer = setTimeout(loadRegistry, 280);
    });
    document.getElementById('correct-form')?.addEventListener('submit', submitCorrection);
  }

  function dateQuery() {
    const from = document.getElementById('cons-from').value;
    const to = document.getElementById('cons-to').value;
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    return params;
  }

  async function refreshAll() {
    const params = dateQuery();
    const qs = params.toString();
    const [summary, aggregates, trend, anomalies] = await Promise.all([
      WW.get('/api/consumption/summary?' + qs),
      WW.get('/api/consumption/aggregates?dimension=' + dimension + '&' + qs),
      WW.get('/api/consumption/trend?' + qs),
      WW.get('/api/consumption/anomalies?' + qs)
    ]);
    renderSummary(summary);
    renderAggregates(aggregates);
    if (trend.length > 1) { trendMonths = trend.map(t => t.period); populatePlaybackMonths(); }
    renderTrend(trend);
    renderAnomalies(anomalies);
    await loadChoropleth();
    await loadRegistry();
  }

  function renderSummary(s) {
    document.getElementById('cons-total').innerHTML = WW.fmt(s.totalM3, 0) + ' <small>m³</small>';
    document.getElementById('cons-total-foot').textContent = s.periodStart ? 'Period ' + s.periodStart + ' → ' + (s.periodEnd || 'latest') : s.coverageMessage;
    document.getElementById('cons-litres').innerHTML = WW.fmt(s.totalLitres, 0) + ' <small>L</small>';
    document.getElementById('cons-estimated').innerHTML = WW.fmt(s.estimatedSharePercent, 1) + '<small>%</small>';
    document.getElementById('cons-anomalies').textContent = WW.fmtInt(s.anomalyCount);
    if (!s.readings) {
      document.getElementById('cons-estimated').parentElement.querySelector('.kpi-foot').textContent = 'No calculated consumption intervals';
    }
  }

  function renderAggregates(rows) {
    document.getElementById('cons-agg-sub').textContent = rows.length ? rows.length + ' groups' : 'No data';
    document.getElementById('cons-aggregates').innerHTML = rows.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Group</th><th class="num">Consumption m³</th><th class="num">Litres</th><th class="num">Accounts</th><th class="num">Population</th><th class="num">LPCD</th><th>Classification</th></tr></thead><tbody>' +
        rows.map(r => '<tr><td>' + WW.escape(r.label) + (r.code ? '<div class="tiny muted">' + WW.escape(r.code) + '</div>' : '') + '</td>' +
          '<td class="num">' + WW.fmt(r.consumptionM3, 1) + '</td><td class="num">' + WW.fmt(r.consumptionLitres, 0) + '</td>' +
          '<td class="num">' + WW.fmtInt(r.accounts) + '</td><td class="num">' + (r.population ? WW.fmtInt(r.population) : '<span class="muted" title="Population not available">—</span>') + '</td>' +
          '<td class="num">' + (r.litresPerCapitaPerDay != null ? WW.fmt(r.litresPerCapitaPerDay, 1) : '<span class="muted">—</span>') + '</td>' +
          '<td>' + WW.classification(r.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No consumption aggregates', 'Consumption requires meter readings and attributed consumer accounts.', 'fa-tint');
  }

  function renderTrend(trend) {
    if (!trend.length) {
      document.getElementById('cons-trend-wrap').innerHTML = WW.empty('No trend', 'No intervals can be calculated for this period.', 'fa-line-chart');
      return;
    }
    WW.chart('cons-trend', {
      type: 'line',
      data: {
        labels: trend.map(t => t.period),
        datasets: [
          WW.lineDataset('Consumption (m³)', trend.map(t => Number(t.consumptionM3)), '#0f8b8d', true),
          WW.lineDataset('Estimated (m³)', trend.map(t => Number(t.estimatedM3)), '#b7791f', false)
        ]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { x: WW.axis(''), y: WW.axis('m³') } }
    });
  }

  function populatePlaybackMonths() {
    const select = document.getElementById('cons-playback-month');
    const prev = select.value;
    select.innerHTML = trendMonths.map(m => '<option value="' + m + '">' + m + '</option>').join('');
    if (trendMonths.includes(prev)) select.value = prev;
  }

  function applyPlaybackMonth() {
    const month = document.getElementById('cons-playback-month').value;
    if (!month) return;
    const start = month + '-01';
    const date = new Date(month + '-01T00:00:00');
    const end = new Date(date.getFullYear(), date.getMonth() + 1, 0).toISOString().slice(0, 10);
    document.getElementById('cons-from').value = start;
    document.getElementById('cons-to').value = end;
    document.getElementById('cons-play-status').textContent = 'Showing ' + month + ' only.';
    refreshAll();
  }

  function togglePlayback() {
    const button = document.getElementById('cons-play');
    if (playbackTimer) {
      clearInterval(playbackTimer); playbackTimer = null;
      button.innerHTML = '<i class="fa fa-play"></i> Play';
      document.getElementById('cons-play-status').textContent = 'Playback paused.';
      return;
    }
    if (!trendMonths.length) return;
    const select = document.getElementById('cons-playback-month');
    let index = Math.max(0, trendMonths.indexOf(select.value));
    if (index >= trendMonths.length - 1) index = 0;
    select.value = trendMonths[index];
    applyPlaybackMonth();
    button.innerHTML = '<i class="fa fa-pause"></i> Pause';
    playbackTimer = setInterval(() => {
      index++;
      if (index >= trendMonths.length) {
        clearInterval(playbackTimer); playbackTimer = null;
        button.innerHTML = '<i class="fa fa-play"></i> Play';
        document.getElementById('cons-play-status').textContent = 'Playback complete.';
        return;
      }
      select.value = trendMonths[index];
      applyPlaybackMonth();
    }, 1800);
  }

  function renderAnomalies(rows) {
    document.getElementById('cons-anomaly-list').innerHTML = rows.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Meter / zone</th><th class="num">Consumption</th><th>Against median</th><th>Date</th></tr></thead><tbody>' +
        rows.slice(0, 15).map(a => '<tr><td><strong>' + WW.escape(a.meterSerial) + '</strong><div class="tiny muted">' + WW.escape(a.zone || 'Unattributed') + '</div></td>' +
          '<td class="num">' + WW.fmt(a.consumptionM3, 1) + ' m³</td><td class="small">' + WW.escape(a.reason) + '</td><td class="nowrap">' + WW.fmtDate(a.readingDate) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No anomalies', 'A meter needs at least three intervals before a baseline anomaly test is applied.', 'fa-check');
  }

  async function loadChoropleth() {
    const params = dateQuery();
    params.set('dimension', dimension);
    try {
      const data = await WW.get('/api/consumption/choropleth?' + params.toString());
      choroplethLayer.clearLayers();
      choroplethLayer.addData(data);
      choroplethLayer.eachLayer(layer => {
        layer.on('click', () => layer.bindPopup(choroplethPopup(layer.feature)).openPopup());
      });
      const values = (data.features || []).map(f => Number(f.properties.consumptionM3 || 0));
      updateLegend(values);
      if (choroplethLayer.getBounds().isValid()) map.fitBounds(choroplethLayer.getBounds(), { padding: [18, 18] });
    } catch (e) {
      WW.toast(e.message, 'warn', 'Choropleth');
    }
  }

  function styleFor(feature) {
    const value = Number(feature.properties.consumptionM3 || 0);
    const max = window.__consMax || 1;
    const ratio = Math.max(0, Math.min(1, value / max));
    const color = `hsl(${192 - ratio * 30}, ${55 + ratio * 20}%, ${88 - ratio * 45}%)`;
    return { color: '#ffffff', weight: 1.5, fillColor: color, fillOpacity: 0.8 };
  }

  function choroplethPopup(feature) {
    const p = feature.properties || {};
    return '<div class="popup-title">' + WW.escape(p.name || 'Area') + '</div>' +
      '<div class="popup-row"><span>Consumption</span><span>' + WW.fmt(p.consumptionM3, 1) + ' m³</span></div>' +
      '<div class="popup-row"><span>Litres</span><span>' + WW.fmt(p.consumptionLitres, 0) + ' L</span></div>' +
      (p.population ? '<div class="popup-row"><span>Population</span><span>' + WW.fmtInt(p.population) + '</span></div>' : '') +
      '<div class="popup-row"><span>Classification</span><span>' + WW.escape(p.classification || '—') + '</span></div>';
  }

  function updateLegend(values) {
    const max = Math.max(1, ...values);
    window.__consMax = max;
    if (legendControl) map.removeControl(legendControl);
    legendControl = L.control({ position: 'bottomright' });
    legendControl.onAdd = function () {
      const div = L.DomUtil.create('div', 'gis-legend');
      const steps = [0.1, 0.25, 0.5, 0.75, 1];
      div.innerHTML = '<h4>Consumption (m³)</h4>' + steps.map(s => '<div class="legend-row"><span class="legend-swatch" style="background:hsl(' + (192 - s * 30) + ',' + (55 + s * 20) + '%,' + (88 - s * 45) + '%)"></span><span>' + WW.fmt(max * s, 0) + '</span></div>').join('');
      return div;
    };
    legendControl.addTo(map);
    choroplethLayer.setStyle(styleFor);
  }

  async function loadRegistry() {
    const search = document.getElementById('registry-search').value.trim();
    const target = document.getElementById('registry-table');
    target.innerHTML = '<div class="loading-block"><div class="skeleton"></div><div class="skeleton"></div></div>';
    const q = new URLSearchParams({ page: '1', pageSize: '100' });
    if (search) q.set('search', search);
    try {
      if (registryTab === 'accounts') {
        const data = await WW.get('/api/consumption/accounts?' + q.toString());
        target.innerHTML = data.items.length ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Account</th><th>Category</th><th>Service zone / DMA</th><th class="num">Meters</th><th>Classification</th></tr></thead><tbody>' +
          data.items.map(a => '<tr><td><strong>' + WW.escape(a.accountNumber) + '</strong><div class="tiny muted">' + WW.escape(a.name) + '</div></td><td>' + WW.escape(a.category) + '</td>' +
            '<td class="small">' + WW.escape(a.serviceZone || '—') + '<div class="tiny muted">' + WW.escape(a.dma || '—') + '</div></td><td class="num">' + a.meterCount + '</td><td>' + WW.classification(a.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
          : WW.empty('No accounts', 'Import an authorised consumer register or enable demonstration data.', 'fa-users');
      } else if (registryTab === 'meters') {
        const data = await WW.get('/api/consumption/meters?' + q.toString());
        target.innerHTML = data.items.length ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Serial</th><th>Account</th><th>Status</th><th class="num">Multiplier</th><th>Last reading</th><th>Classification</th></tr></thead><tbody>' +
          data.items.map(m => '<tr><td><strong>' + WW.escape(m.serialNumber) + '</strong></td><td>' + WW.escape(m.accountNumber || '—') + '<div class="tiny muted">' + WW.escape(m.consumerName || '') + '</div></td><td>' + WW.status(m.status) + '</td>' +
            '<td class="num">' + m.multiplier + '</td><td>' + WW.fmtDate(m.lastReading) + ' · ' + WW.fmt(m.lastValueM3, 1) + ' m³</td><td>' + WW.classification(m.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
          : WW.empty('No meters', 'Register meters before importing readings.', 'fa-tachometer');
      } else {
        const data = await WW.get('/api/consumption/readings?page=1&pageSize=200');
        target.innerHTML = data.items.length ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Meter</th><th>Date</th><th class="num">Reading m³</th><th>Type</th><th>Classification</th><th></th></tr></thead><tbody>' +
          data.items.map(r => '<tr><td>' + WW.escape(r.meterSerial) + '<div class="tiny muted">' + WW.escape(r.accountNumber || '') + '</div></td><td>' + WW.fmtDate(r.readingDate) + '</td><td class="num">' + WW.fmt(r.readingValueM3, 3) + '</td><td>' + WW.status(r.readingType) + '</td><td>' + WW.classification(r.dataClassification) + '</td>' +
            '<td class="actions">' + (canCorrect ? '<button class="btn btn-sm" type="button" data-correct="' + r.id + '" data-value="' + r.readingValueM3 + '">Correct</button>' : '') + '</td></tr>').join('') + '</tbody></table></div>'
          : WW.empty('No readings', 'Import a reading CSV.', 'fa-list');
        target.querySelectorAll('[data-correct]').forEach(btn => btn.addEventListener('click', () => {
          document.getElementById('correct-id').value = btn.dataset.correct;
          document.getElementById('correct-value').value = btn.dataset.value;
          document.getElementById('correct-reason').value = '';
          WW.modal.open('correct-modal');
        }));
      }
    } catch (e) {
      target.innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>';
    }
  }

  async function runImport() {
    const fileInput = document.getElementById('readings-file');
    const commit = document.getElementById('readings-commit').checked;
    if (!fileInput.files.length) { WW.toast('Choose a CSV file.', 'warn'); return; }
    const fd = new FormData();
    fd.append('file', fileInput.files[0]);
    fd.append('commit', commit ? 'true' : 'false');
    const button = document.getElementById('run-import');
    button.disabled = true;
    try {
      const result = await WW.post('/api/consumption/readings/import', fd);
      document.getElementById('import-result').innerHTML =
        '<div class="alert alert-' + (result.rowsRejected > 0 ? 'warn' : 'success') + '"><i class="fa fa-info-circle"></i><div><strong>' + WW.escape(result.status) + '</strong> · ' +
        result.rowsAccepted + ' accepted / ' + result.rowsTotal + ' rows.' + (result.periodStart ? ' Period ' + result.periodStart + ' → ' + result.periodEnd + '.' : '') + '</div></div>' +
        (result.errors.length ? '<div class="table-wrap mt-2"><table class="ww-table"><thead><tr><th>Row</th><th>Message</th></tr></thead><tbody>' +
          result.errors.slice(0, 30).map(e => '<tr><td>' + e.row + '</td><td>' + WW.escape(e.message) + '</td></tr>').join('') + '</tbody></table></div>' : '');
      if (commit) await refreshAll();
    } catch (e) {
      document.getElementById('import-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>';
    } finally { button.disabled = false; }
  }

  async function submitCorrection(e) {
    e.preventDefault();
    const id = document.getElementById('correct-id').value;
    try {
      await WW.post('/api/consumption/readings/' + id + '/correct', {
        valueM3: Number(document.getElementById('correct-value').value),
        reason: document.getElementById('correct-reason').value
      });
      WW.modal.close('correct-modal');
      WW.toast('Reading corrected.', 'warn', 'Saved');
      await refreshAll();
    } catch (ex) { WW.toast(ex.message, 'error', 'Correction'); }
  }
})();
