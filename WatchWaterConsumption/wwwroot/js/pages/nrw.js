(function () {
  'use strict';
  const canManage = document.getElementById('nrw-permissions')?.dataset.canManage === 'true';
  const canField = document.getElementById('nrw-permissions')?.dataset.canField === 'true';
  let dmas = [];
  let leaks = [];
  const map = L.map('nrw-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  const dmaLayer = L.geoJSON(null, { style: styleDma }).addTo(map);
  const leakLayer = L.layerGroup().addTo(map);

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    bind();
    await loadBase();
    await refresh();
  }

  function bind() {
    document.getElementById('new-balance')?.addEventListener('click', () => WW.modal.open('balance-modal'));
    document.getElementById('new-leak')?.addEventListener('click', () => WW.modal.open('leak-modal'));
    document.getElementById('balance-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/nrw/balances', {
          dmaId: data.get('dmaId'), periodStart: data.get('periodStart'), periodEnd: data.get('periodEnd'),
          inputVolumeM3: Number(data.get('inputVolumeM3')), billedVolumeM3: Number(data.get('billedVolumeM3')),
          unbilledAuthorisedM3: num(data.get('unbilledAuthorisedM3')), apparentLossesM3: num(data.get('apparentLossesM3')),
          realLossesM3: num(data.get('realLossesM3')), minimumNightFlowM3PerHour: num(data.get('minimumNightFlowM3PerHour')),
          method: data.get('method') || null, uncertaintyNotes: data.get('uncertaintyNotes') || null
        });
        WW.modal.close('balance-modal'); WW.toast('Water balance saved.', 'warn', 'Saved'); await refresh();
      } catch (ex) { WW.toast(ex.message, 'error', 'Water balance'); }
    });
    document.getElementById('leak-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/nrw/leaks', {
          description: data.get('description'), dmaId: data.get('dmaId') || null,
          latitude: num(data.get('latitude')), longitude: num(data.get('longitude')),
          severity: data.get('severity'), estimatedLossM3PerHour: num(data.get('estimatedLossM3PerHour'))
        });
        WW.modal.close('leak-modal'); WW.toast('Leak reported.', 'warn', 'Saved'); await refresh();
      } catch (ex) { WW.toast(ex.message, 'error', 'Leak report'); }
    });
  }

  function num(v) { return v === null || v === undefined || v === '' ? null : Number(v); }

  async function loadBase() {
    try {
      const [ctx, zones] = await Promise.all([WW.get('/api/geography/context'), WW.get('/api/layers/operational-zones/geojson')]);
      dmaLayer.addData(zones);
      dmaLayer.eachLayer(layer => {
        if (layer.feature.properties.areaType !== 'DistrictMeteredArea') {
          layer.setStyle({ opacity: 0, fillOpacity: 0 });
          if (layer._path) layer._path.style.pointerEvents = 'none';
        }
      });
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [18, 18] });
      WW.get('/api/layers/admin-lga/geojson').then(data => L.geoJSON(data, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0 } }).addTo(map)).catch(() => { });
    } catch { }
  }

  function styleDma(feature) {
    const latest = window.__dmaNrw?.[feature.properties.id] || 0;
    const ratio = Math.min(1, latest / 60);
    return { color: '#fff', weight: 1.2, fillColor: 'hsl(' + (45 - ratio * 45) + ',80%,' + (82 - ratio * 35) + '%)', fillOpacity: 0.72 };
  }

  async function refresh() {
    try {
      const [summary, balances, trend, leakData, hotspots, pressure, dmaData] = await Promise.all([
        WW.get('/api/nrw/summary'), WW.get('/api/nrw/balances'), WW.get('/api/nrw/trend'),
        WW.get('/api/nrw/leaks'), WW.get('/api/nrw/hotspots'), WW.get('/api/nrw/pressure-anomalies'), WW.get('/api/nrw/dmas')
      ]);
      dmas = dmaData; leaks = leakData;
      document.getElementById('nrw-percent').innerHTML = WW.fmt(summary.nrwPercent, 1) + '<small>%</small>';
      document.getElementById('nrw-m3').textContent = WW.fmt(summary.nrwM3, 0) + ' m³ non-revenue';
      document.getElementById('nrw-input').textContent = WW.fmt(summary.inputM3, 0);
      document.getElementById('nrw-billed').textContent = WW.fmt(summary.billedM3, 0);
      document.getElementById('nrw-leaks').textContent = WW.fmtInt(summary.openLeaks);
      document.getElementById('nrw-leak-loss').textContent = WW.fmt(summary.estimatedLeakLossM3PerHour, 2) + ' m³/h estimated';
      document.getElementById('nrw-availability').innerHTML = '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>' + WW.escape(summary.availabilityMessage) + '</div></div>';
      renderBalances(balances);
      renderTrend(trend);
      renderLeaks(leakData);
      renderHotspots(hotspots);
      renderPressure(pressure);
      populateSelects();
      const mapData = {};
      dmas.forEach(d => { if (d.latestNrwPercent != null) mapData[d.id] = d.latestNrwPercent; });
      window.__dmaNrw = mapData;
      dmaLayer.setStyle(styleDma);
      leakLayer.clearLayers();
      leakData.filter(l => l.longitude != null).forEach(l => {
        L.circleMarker([l.latitude, l.longitude], { radius: 6, color: '#fff', weight: 2, fillColor: l.status === 'Reported' ? '#c0392b' : '#b7791f', fillOpacity: 0.9 })
          .addTo(leakLayer).bindPopup('<div class="popup-title">' + WW.escape(l.reference) + '</div><div class="popup-row"><span>DMA</span><span>' + WW.escape(l.dmaName || '—') + '</span></div><div class="popup-row"><span>Status</span><span>' + WW.escape(l.status) + '</span></div><div class="popup-row"><span>Estimated loss</span><span>' + (l.estimatedLossM3PerHour ?? '—') + ' m³/h</span></div>');
      });
    } catch (e) { WW.toast(e.message, 'error', 'NRW'); }
  }

  function renderBalances(items) {
    document.getElementById('nrw-balance-count').textContent = items.length + ' balances';
    document.getElementById('nrw-balances').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>DMA / period</th><th class="num">Input</th><th class="num">Billed</th><th class="num">NRW</th><th class="num">NRW %</th><th class="num">Unallocated</th><th>Evidence</th></tr></thead><tbody>' +
        items.slice(0, 40).map(b => '<tr><td><strong>' + WW.escape(b.dmaName) + '</strong><div class="tiny muted">' + b.periodStart + ' → ' + b.periodEnd + '</div></td><td class="num">' + WW.fmt(b.inputVolumeM3, 0) + '</td><td class="num">' + WW.fmt(b.billedVolumeM3, 0) + '</td><td class="num">' + WW.fmt(b.nonRevenueWaterM3, 0) + '</td><td class="num">' + WW.fmt(b.nonRevenueWaterPercent, 1) + '</td><td class="num">' + WW.fmt(b.unallocatedLossM3, 0) + '</td><td><span class="badge ' + (b.lossSplitEvidence === 'Evidenced' ? 'badge-green' : b.lossSplitEvidence === 'Partial' ? 'badge-amber' : 'badge-grey') + '">' + WW.escape(b.lossSplitEvidence) + '</span></td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No balances', 'Record system input and billed authorised consumption for a DMA.', 'fa-balance-scale');
  }

  function renderTrend(trend) {
    if (!trend.length) { document.getElementById('nrw-trend').parentElement.innerHTML = WW.empty('No trend', 'Record DMA balances over time.', 'fa-line-chart'); return; }
    WW.chart('nrw-trend', {
      type: 'line',
      data: { labels: trend.map(t => t.period), datasets: [WW.lineDataset('NRW %', trend.map(t => Number(t.nrwPercent)), '#b7791f', true)] },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: WW.axis(''), y: WW.axis('%') } }
    });
  }

  function renderLeaks(items) {
    const next = { Reported: 'Triaged', Triaged: 'Assigned', Assigned: 'InProgress', InProgress: 'Repaired', Repaired: 'Verified', Verified: 'Closed' };
    document.getElementById('nrw-leaks-list').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Reference</th><th>DMA</th><th>Status</th><th class="num">Est. loss</th><th></th></tr></thead><tbody>' +
        items.slice(0, 30).map(l => '<tr><td><strong>' + WW.escape(l.reference) + '</strong><div class="tiny muted">' + WW.fmtDate(l.reportedAt) + ' · ' + WW.escape(l.severity) + '</div></td><td>' + WW.escape(l.dmaName || '—') + '</td><td>' + WW.status(l.status) + '</td><td class="num">' + (l.estimatedLossM3PerHour ?? '—') + ' m³/h</td><td class="actions">' + (canField && next[l.status] ? '<button class="btn btn-sm" data-leak="' + l.id + '" data-status="' + next[l.status] + '">Advance</button>' : '') + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No leak incidents', 'Reported leaks will appear here.', 'fa-check');
    document.querySelectorAll('[data-leak]').forEach(btn => btn.addEventListener('click', async () => {
      try {
        await WW.post('/api/nrw/leaks/' + btn.dataset.leak + '/status', { status: btn.dataset.status });
        WW.toast('Leak status advanced to ' + btn.dataset.status + '.', 'warn', 'Saved'); await refresh();
      } catch (e) { WW.toast(e.message, 'error', 'Leak status'); }
    }));
  }

  function renderHotspots(items) {
    document.getElementById('nrw-hotspots').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>DMA</th><th class="num">Incidents</th><th class="num">Open</th><th class="num">Est. loss</th></tr></thead><tbody>' +
        items.map(h => '<tr><td>' + WW.escape(h.dmaName) + '</td><td class="num">' + h.incidents + '</td><td class="num">' + h.openIncidents + '</td><td class="num">' + WW.fmt(h.estimatedLossM3PerHour, 2) + ' m³/h</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No hotspots', 'Leak incidents are required for hotspot ranking.', 'fa-map-marker');
  }

  function renderPressure(items) {
    document.getElementById('nrw-pressure').innerHTML = items.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Device</th><th class="num">Value</th><th>Triggered</th><th>Severity</th></tr></thead><tbody>' +
        items.map(a => '<tr><td>' + WW.escape(a.deviceName) + '<div class="tiny muted">' + WW.escape(a.deviceCode) + '</div></td><td class="num">' + (a.value ?? '—') + '</td><td>' + WW.fmtRelative(a.triggeredAt) + '</td><td>' + WW.status(a.severity) + '</td></tr>').join('') + '</tbody></table></div>'
      : WW.empty('No pressure anomalies', 'Rule-based pressure alerts will appear here.', 'fa-check');
  }

  function populateSelects() {
    const balance = document.getElementById('balance-dma');
    balance.innerHTML = dmas.map(d => '<option value="' + d.id + '">' + WW.escape(d.name) + '</option>').join('');
    const leak = document.getElementById('leak-dma');
    leak.innerHTML = '<option value="">Not assigned</option>' + dmas.map(d => '<option value="' + d.id + '">' + WW.escape(d.name) + '</option>').join('');
  }
})();
