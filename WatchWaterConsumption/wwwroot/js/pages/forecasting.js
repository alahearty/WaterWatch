(function () {
  'use strict';
  let overview = null;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    try {
      overview = await WW.get('/api/forecasting/overview');
      document.getElementById('forecast-availability').innerHTML = '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>' + WW.escape(overview.availabilityMessage) + '</div></div>';
      renderDemand(overview.demand);
      renderModelCard(overview.demand.metadata);
      renderRisk(overview.assetRisks);
      renderLeakage(overview.leakagePriorities);
      renderQuality(overview.qualityTrends);
      await loadBoreholes();
    } catch (e) {
      document.getElementById('forecast-availability').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>';
    }
  }

  function renderDemand(demand) {
    const wrap = document.getElementById('demand-chart-wrap');
    if (!demand.points.length) { wrap.innerHTML = WW.empty('Forecast unavailable', demand.metadata.message || 'Insufficient data.', 'fa-line-chart'); return; }
    wrap.innerHTML = '<canvas id="demand-chart-canvas"></canvas>';
    const labels = demand.points.map(p => p.period);
    const historical = demand.points.map(p => p.isForecast ? null : p.value);
    const forecast = demand.points.map(p => p.isForecast ? p.value : null);
    const lower = demand.points.map(p => p.isForecast ? p.lower : null);
    const upper = demand.points.map(p => p.isForecast ? p.upper : null);
    WW.chart('demand-chart-canvas', {
      type: 'line',
      data: { labels, datasets: [
        WW.lineDataset('Historical (m³)', historical, '#0f8b8d', true),
        WW.lineDataset('Forecast (m³)', forecast, '#6d5bd0', false),
        { label: '95% lower', data: lower, borderColor: '#c4b5fd', borderDash: [4, 4], borderWidth: 1, pointRadius: 0, fill: false },
        { label: '95% upper', data: upper, borderColor: '#c4b5fd', borderDash: [4, 4], borderWidth: 1, pointRadius: 0, fill: false }
      ] },
      options: { responsive: true, maintainAspectRatio: false, spanGaps: true, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { x: WW.axis(''), y: WW.axis(demand.unit) } }
    });
  }

  function renderModelCard(m) {
    const metrics = Object.entries(m.metrics || {}).filter(([, v]) => v != null);
    document.getElementById('demand-model-card').innerHTML =
      '<div class="kv"><dt>Status</dt><dd>' + (m.status === 'available' ? '<span class="badge badge-green">Available</span>' : '<span class="badge badge-amber">Insufficient data</span>') + '</dd></div>' +
      '<div class="kv"><dt>Model</dt><dd>' + WW.escape(m.modelName) + ' · ' + WW.escape(m.modelVersion) + '</dd></div>' +
      '<div class="kv"><dt>Training</dt><dd>' + WW.fmtDate(m.trainingStart) + ' → ' + WW.fmtDate(m.trainingEnd) + '</dd></div>' +
      '<div class="kv"><dt>Observations</dt><dd>' + m.observationCount + '</dd></div>' +
      '<div class="kv"><dt>Validation</dt><dd class="small">' + WW.escape(m.validationMethod) + '</dd></div>' +
      (metrics.length ? '<div class="kv"><dt>Metrics</dt><dd class="small">' + metrics.map(([k, v]) => WW.escape(k) + ': ' + WW.fmt(v, 3)).join('<br>') + '</dd></div>' : '') +
      '<div class="kv"><dt>Confidence</dt><dd class="small">' + WW.escape(m.confidenceNote || 'Not stated') + '</dd></div>' +
      '<div class="kv"><dt>Limitations</dt><dd class="small">' + WW.escape(m.limitations) + '</dd></div>' +
      '<div class="kv"><dt>Generated</dt><dd>' + WW.fmtDate(m.generatedAt, true) + '</dd></div>' +
      '<div class="mt-1">' + WW.classification(m.dataClassification) + '</div>' +
      (m.message ? '<div class="alert alert-warn mt-2"><i class="fa fa-info-circle"></i><div>' + WW.escape(m.message) + '</div></div>' : '');
  }

  function renderRisk(items) {
    document.getElementById('asset-risk').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Asset</th><th>Risk</th><th class="num">Score</th><th>Factors</th><th>Class</th></tr></thead><tbody>' +
        items.map(r => '<tr><td><a href="/Infrastructure/Details/' + r.assetId + '">' + WW.escape(r.name) + '</a><div class="tiny muted">' + WW.escape(r.assetType) + ' · ' + WW.escape(r.condition) + '</div></td><td><span class="badge ' + (r.riskBand === 'High' ? 'badge-red' : r.riskBand === 'Medium' ? 'badge-amber' : 'badge-green') + '">' + WW.escape(r.riskBand) + '</span></td><td class="num">' + r.riskScore + '</td><td class="small">' + r.factors.map(WW.escape).join('; ') + '</td><td>' + WW.classification(r.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No asset risk', 'Register assets with condition/installation data.', 'fa-wrench');
  }

  function renderLeakage(items) {
    document.getElementById('leak-priority').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>DMA</th><th class="num">NRW</th><th class="num">Open leaks</th><th>Priority</th><th class="num">Score</th></tr></thead><tbody>' +
        items.map(r => '<tr><td>' + WW.escape(r.dmaName) + '<div class="tiny muted">' + r.factors.map(WW.escape).join(' · ') + '</div></td><td class="num">' + (r.nrwPercent != null ? WW.fmt(r.nrwPercent, 1) + '%' : '—') + '</td><td class="num">' + r.openLeaks + '</td><td><span class="badge ' + (r.priority === 'Investigate now' ? 'badge-red' : r.priority === 'Monitor' ? 'badge-amber' : 'badge-green') + '">' + WW.escape(r.priority) + '</span></td><td class="num">' + r.riskScore + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No leakage priority', 'DMA water balances are required.', 'fa-chain-broken');
  }

  function renderQuality(items) {
    document.getElementById('quality-deterioration').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Station</th><th>Parameter</th><th class="num">Slope / month</th><th>Direction</th><th class="num">n</th></tr></thead><tbody>' +
        items.map(r => '<tr><td><a href="/WaterQuality/Station/' + r.stationId + '">' + WW.escape(r.stationName) + '</a></td><td>' + WW.escape(r.parameter) + '</td><td class="num">' + WW.fmt(r.slopePerMonth, 4) + '</td><td><span class="badge ' + (r.direction === 'Increasing' ? 'badge-red' : r.direction === 'Decreasing' ? 'badge-green' : 'badge-grey') + '">' + WW.escape(r.direction) + '</span></td><td class="num">' + r.observations + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No quality trend', 'At least four results per station and parameter are required.', 'fa-flask');
  }

  async function loadBoreholes() {
    try {
      const boreholes = await WW.get('/api/groundwater/boreholes');
      document.getElementById('forecast-borehole').innerHTML = boreholes.map(b => '<option value="' + b.assetId + '">' + WW.escape(b.name) + '</option>').join('') || '<option value="">No boreholes</option>';
      document.getElementById('forecast-water-level').addEventListener('click', runWaterLevel);
      if (boreholes.length) runWaterLevel();
    } catch { }
  }

  async function runWaterLevel() {
    const id = document.getElementById('forecast-borehole').value;
    if (!id) return;
    try {
      const forecast = await WW.get('/api/forecasting/water-level?boreholeAssetId=' + id);
      const wrap = document.getElementById('water-level-chart-wrap');
      if (!forecast.points.length) {
        wrap.innerHTML = WW.empty('Forecast unavailable', forecast.metadata.message || 'Insufficient readings.', 'fa-line-chart');
      } else {
        wrap.innerHTML = '<canvas id="water-level-chart-canvas"></canvas>';
        const labels = forecast.points.map(p => p.period);
        WW.chart('water-level-chart-canvas', {
          type: 'line',
          data: { labels, datasets: [
            WW.lineDataset('Historical (m)', forecast.points.map(p => p.isForecast ? null : p.value), '#0369a1', true),
            WW.lineDataset('Forecast (m)', forecast.points.map(p => p.isForecast ? p.value : null), '#6d5bd0', false),
            { label: 'Lower', data: forecast.points.map(p => p.isForecast ? p.lower : null), borderColor: '#c4b5fd', borderDash: [4, 4], borderWidth: 1, pointRadius: 0, fill: false },
            { label: 'Upper', data: forecast.points.map(p => p.isForecast ? p.upper : null), borderColor: '#c4b5fd', borderDash: [4, 4], borderWidth: 1, pointRadius: 0, fill: false }
          ] },
          options: { responsive: true, maintainAspectRatio: false, spanGaps: true, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { x: WW.axis(''), y: WW.axis('m') } }
        });
      }
      document.getElementById('water-level-card').innerHTML = '<div class="tiny muted"><strong>' + WW.escape(forecast.metadata.modelName) + ' ' + WW.escape(forecast.metadata.modelVersion) + '</strong> · ' + WW.escape(forecast.metadata.validationMethod) + ' · ' + WW.escape(forecast.metadata.limitations) + '</div>';
    } catch (e) { document.getElementById('water-level-card').innerHTML = '<div class="alert alert-warn">' + WW.escape(e.message) + '</div>'; }
  }
})();
