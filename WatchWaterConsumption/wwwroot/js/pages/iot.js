(function () {
  'use strict';
  const canManage = document.getElementById('iot-permissions')?.dataset.canManage === 'true';
  let devices = [];
  let alerts = [];
  let liveCount = 0;
  let reloadTimer = null;
  let connection = null;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    bind();
    await Promise.all([loadSummary(), loadDevices(), loadAlerts(), loadRules()]);
    connectSignalR();
  }

  function bind() {
    document.getElementById('new-device')?.addEventListener('click', () => WW.modal.open('device-modal'));
    document.getElementById('new-rule')?.addEventListener('click', () => WW.modal.open('rule-modal'));
    document.getElementById('escalate-alerts').addEventListener('click', async () => {
      try { const result = await WW.post('/api/telemetry/alerts/escalate', {}); WW.toast(result.escalated + ' alert(s) escalated.', 'warn', 'Alerts'); await loadAlerts(); }
      catch (e) { WW.toast(e.message, 'error', 'Alerts'); }
    });
    document.getElementById('iot-device').addEventListener('change', loadDeviceHistory);
    document.getElementById('iot-parameter').addEventListener('change', loadDeviceHistory);
    document.getElementById('iot-window').addEventListener('change', loadDeviceHistory);
    document.getElementById('device-form')?.addEventListener('submit', createDevice);
    document.getElementById('rule-form')?.addEventListener('submit', createRule);
  }

  async function loadSummary() {
    try {
      const s = await WW.get('/api/telemetry/summary');
      document.getElementById('iot-devices').textContent = WW.fmtInt(s.devices);
      document.getElementById('iot-demo').textContent = s.demoDevices + ' demo devices';
      document.getElementById('iot-active').textContent = WW.fmtInt(s.activeDevices);
      document.getElementById('iot-offline').textContent = s.offlineDevices + ' offline';
      document.getElementById('iot-readings').textContent = WW.fmtInt(s.readings);
      document.getElementById('iot-last').textContent = s.lastReadingAt ? 'Last ' + WW.fmtRelative(s.lastReadingAt) : 'No readings';
      document.getElementById('iot-alerts').textContent = WW.fmtInt(s.openAlerts);
      document.getElementById('iot-availability').innerHTML = '<div class="alert ' + (s.demoMode ? 'alert-warn' : 'alert-info') + '"><i class="fa ' + (s.demoMode ? 'fa-flask' : 'fa-info-circle') + '"></i><div>' + WW.escape(s.availabilityMessage) + '</div></div>';
    } catch (e) { WW.toast(e.message, 'error', 'Telemetry'); }
  }

  async function loadDevices() {
    try {
      devices = await WW.get('/api/telemetry/devices');
      document.getElementById('iot-device-count').textContent = devices.length + ' registered';
      document.getElementById('iot-device-list').innerHTML = devices.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Device</th><th>Type</th><th>Status</th><th>Freshness</th><th>Last reading</th><th>Class</th></tr></thead><tbody>' +
          devices.map(d => '<tr><td><strong>' + WW.escape(d.name) + '</strong><div class="tiny muted">' + WW.escape(d.deviceCode) + (d.isDemoDevice ? ' · <span class="badge badge-synthetic">DEMO</span>' : '') + '</div></td><td>' + WW.escape(d.deviceType) + '</td><td>' + WW.status(d.status) + '</td><td>' + WW.status(d.freshness) + '</td><td>' + (d.lastValue != null ? WW.escape(d.lastParameter) + ' ' + WW.fmt(d.lastValue, 3) + ' ' + WW.escape(d.lastUnit) + '<div class="tiny muted">' + WW.fmtRelative(d.lastSeenAt) + '</div>' : '—') + '</td><td>' + WW.classification(d.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No devices', 'Register a device and ingest telemetry with its API key.', 'fa-wifi');
      const select = document.getElementById('iot-device');
      const previous = select.value;
      select.innerHTML = devices.map(d => '<option value="' + d.id + '">' + WW.escape(d.name) + '</option>').join('');
      if (previous && devices.some(d => d.id === previous)) select.value = previous;
      await loadParameterOptions();
      await loadDeviceHistory();
    } catch (e) { document.getElementById('iot-device-list').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  async function loadParameterOptions() {
    const deviceId = document.getElementById('iot-device').value;
    if (!deviceId) return;
    try {
      const readings = await WW.get('/api/telemetry/readings?deviceId=' + deviceId + '&limit=500');
      const params = Array.from(new Set(readings.map(r => r.parameter)));
      const select = document.getElementById('iot-parameter');
      const prev = select.value;
      select.innerHTML = params.map(p => '<option>' + WW.escape(p) + '</option>').join('');
      if (params.includes(prev)) select.value = prev;
    } catch { }
  }

  async function loadDeviceHistory() {
    const deviceId = document.getElementById('iot-device').value;
    const parameter = document.getElementById('iot-parameter').value;
    if (!deviceId || !parameter) {
      document.getElementById('iot-chart-wrap').innerHTML = WW.empty('No history', 'Select a device with telemetry.', 'fa-line-chart');
      return;
    }
    const hours = Number(document.getElementById('iot-window').value);
    const from = new Date(Date.now() - hours * 3600 * 1000).toISOString();
    try {
      const readings = await WW.get('/api/telemetry/readings?deviceId=' + deviceId + '&from=' + encodeURIComponent(from) + '&limit=5000');
      const filtered = readings.filter(r => r.parameter === parameter).slice().reverse();
      if (!filtered.length) { document.getElementById('iot-chart-wrap').innerHTML = WW.empty('No readings', 'No readings in the selected window.', 'fa-line-chart'); return; }
      document.getElementById('iot-chart-wrap').innerHTML = '<canvas id="iot-chart-canvas"></canvas>';
      const synthetic = filtered.some(r => r.isSynthetic);
      WW.chart('iot-chart-canvas', {
        type: 'line',
        data: { labels: filtered.map(r => WW.fmtDate(r.observedAt, true)), datasets: [WW.lineDataset(parameter + (synthetic ? ' (synthetic)' : ''), filtered.map(r => Number(r.value)), synthetic ? '#b7791f' : '#0f8b8d', true)] },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { x: { ...WW.axis(''), ticks: { maxRotation: 45, autoSkip: true, maxTicksLimit: 8 } }, y: WW.axis(filtered[0].unit) } }
      });
    } catch (e) { WW.toast(e.message, 'error', 'Telemetry history'); }
  }

  async function loadAlerts() {
    try {
      alerts = await WW.get('/api/telemetry/alerts');
      const open = alerts.filter(a => a.status !== 'Resolved');
      document.getElementById('iot-alert-count').textContent = open.length + ' active';
      document.getElementById('iot-alert-list').innerHTML = alerts.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Rule / device</th><th>Message</th><th>Status</th><th></th></tr></thead><tbody>' +
          alerts.slice(0, 30).map(a => '<tr><td><strong>' + WW.escape(a.ruleName) + '</strong><div class="tiny muted">' + WW.escape(a.deviceName) + ' · ' + WW.fmtRelative(a.triggeredAt) + '</div></td><td class="small">' + WW.escape(a.message || '') + (a.escalationDue ? '<div class="tiny" style="color:#c0392b">Escalation due</div>' : '') + '</td><td>' + WW.status(a.status) + '</td><td class="actions">' + (a.status === 'Open' && canManage ? '<button class="btn btn-sm" data-ack="' + a.id + '">Ack</button>' : '') + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No alerts', 'Alert rules will raise records when thresholds are breached.', 'fa-bell');
      document.querySelectorAll('[data-ack]').forEach(btn => btn.addEventListener('click', async () => {
        try { await WW.post('/api/telemetry/alerts/' + btn.dataset.ack + '/acknowledge', { notes: 'Acknowledged in the operations console.' }); WW.toast('Alert acknowledged.', 'warn', 'Alerts'); await loadAlerts(); await loadSummary(); }
        catch (e) { WW.toast(e.message, 'error', 'Acknowledge'); }
      }));
    } catch (e) { document.getElementById('iot-alert-list').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  async function loadRules() {
    if (!canManage) { document.getElementById('iot-rules').innerHTML = '<div class="alert alert-info m-3">Alert rules are visible to authorised monitoring roles.</div>'; return; }
    try {
      const rules = await WW.get('/api/telemetry/alert-rules');
      document.getElementById('iot-rules').innerHTML = rules.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Rule</th><th>Device type</th><th>Condition</th><th>Severity</th><th class="num">Escalation</th></tr></thead><tbody>' +
          rules.map(r => '<tr><td><strong>' + WW.escape(r.name) + '</strong><div class="tiny muted">' + WW.escape(r.description || '') + '</div></td><td>' + WW.escape(r.deviceType || 'Any') + '</td><td class="mono">' + WW.escape(r.parameter) + ' ' + WW.escape(r.operator) + ' ' + (r.threshold ?? '—') + (r.threshold2 != null ? ' … ' + r.threshold2 : '') + '</td><td>' + WW.status(r.severity) + '</td><td class="num">' + r.escalationMinutes + ' min</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No alert rules', 'Create a threshold rule to activate alerts.', 'fa-bell');
    } catch (e) { document.getElementById('iot-rules').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  async function createDevice(e) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    try {
      const result = await WW.post('/api/telemetry/devices', {
        deviceCode: data.get('deviceCode'), name: data.get('name'), deviceType: data.get('deviceType'),
        latitude: num(data.get('latitude')), longitude: num(data.get('longitude')),
        offlineThresholdMinutes: Number(data.get('offlineThresholdMinutes') || 180),
        firmwareVersion: data.get('firmwareVersion') || null
      });
      WW.modal.close('device-modal');
      document.getElementById('device-key').textContent = result.apiKey;
      document.getElementById('copy-key').onclick = () => { navigator.clipboard?.writeText(result.apiKey); WW.toast('Key copied.', 'warn'); };
      WW.modal.open('device-key-modal');
      await loadDevices(); await loadSummary();
    } catch (ex) { WW.toast(ex.message, 'error', 'Device'); }
  }

  async function createRule(e) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    try {
      await WW.post('/api/telemetry/alert-rules', {
        name: data.get('name'), deviceType: data.get('deviceType') || null, parameter: data.get('parameter'),
        operator: data.get('operator'), threshold: num(data.get('threshold')), threshold2: num(data.get('threshold2')),
        severity: data.get('severity'), escalationMinutes: Number(data.get('escalationMinutes') || 60),
        description: data.get('description') || null
      });
      WW.modal.close('rule-modal'); WW.toast('Alert rule created.', 'warn', 'Saved'); await loadRules();
    } catch (ex) { WW.toast(ex.message, 'error', 'Alert rule'); }
  }

  function connectSignalR() {
    const status = document.getElementById('signalr-status');
    if (!window.signalR) { status.textContent = 'Realtime: unavailable'; return; }
    connection = new signalR.HubConnectionBuilder().withUrl('/hubs/telemetry').withAutomaticReconnect().build();
    connection.on('telemetry', reading => {
      liveCount++;
      document.getElementById('live-count').textContent = liveCount;
      const live = document.getElementById('iot-live');
      const item = document.createElement('div');
      item.className = 'notification';
      item.innerHTML = '<span class="n-icon" style="' + (reading.isSynthetic ? 'background:#fff7e6;color:#9a6700' : '') + '"><i class="fa ' + (reading.isSynthetic ? 'fa-flask' : 'fa-wifi') + '"></i></span><span><strong>' + WW.escape(reading.deviceName) + '</strong><span>' + WW.escape(reading.parameter) + ' = ' + WW.escape(reading.value) + ' ' + WW.escape(reading.unit) + ' · ' + WW.fmtRelative(reading.observedAt) + (reading.isSynthetic ? ' · SYNTHETIC' : '') + '</span></span>';
      live.prepend(item);
      while (live.children.length > 25) live.lastChild.remove();
      clearTimeout(reloadTimer);
      reloadTimer = setTimeout(async () => { await loadSummary(); await loadDevices(); await loadAlerts(); }, 1200);
    });
    connection.on('alert', alert => { WW.toast(alert.ruleName + ': ' + (alert.message || 'threshold breached'), 'error', 'Alert'); loadAlerts(); loadSummary(); });
    connection.on('alertEscalated', alert => { WW.toast(alert.ruleName + ' escalated', 'error', 'Escalation'); loadAlerts(); });
    connection.start()
      .then(() => { status.textContent = 'Realtime: connected'; status.className = 'badge badge-green'; })
      .catch(() => { status.textContent = 'Realtime: disconnected'; status.className = 'badge badge-red'; });
    connection.onreconnecting(() => { status.textContent = 'Realtime: reconnecting'; status.className = 'badge badge-amber'; });
    connection.onreconnected(() => { status.textContent = 'Realtime: connected'; status.className = 'badge badge-green'; });
    connection.onclose(() => { status.textContent = 'Realtime: disconnected'; status.className = 'badge badge-red'; });
  }

  function num(v) { return v === null || v === undefined || v === '' ? null : Number(v); }
})();
