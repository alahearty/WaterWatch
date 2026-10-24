(function () {
  'use strict';
  const canManage = document.getElementById('ops-permissions')?.dataset.canManage === 'true';
  const transitions = {
    Reported: ['Triaged', 'Cancelled'], Triaged: ['Assigned', 'Cancelled'], Assigned: ['InProgress', 'Triaged', 'Cancelled'],
    InProgress: ['Resolved', 'Assigned'], Resolved: ['Verified', 'InProgress'], Verified: ['Closed'], Closed: [], Cancelled: []
  };
  let currentOrder = null;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    bind();
    await loadAssets();
    await refresh();
  }

  function bind() {
    document.getElementById('new-work-order')?.addEventListener('click', () => WW.modal.open('ops-create-modal'));
    document.getElementById('ops-status').addEventListener('change', loadOrders);
    document.getElementById('ops-priority').addEventListener('change', loadOrders);
    document.getElementById('ops-type').addEventListener('change', loadOrders);
    document.getElementById('ops-refresh').addEventListener('click', refresh);
    document.getElementById('ops-create-form')?.addEventListener('submit', createOrder);
    document.getElementById('ops-transition-form')?.addEventListener('submit', submitTransition);
  }

  async function loadAssets() {
    try {
      const data = await WW.get('/api/assets?page=1&pageSize=200');
      document.getElementById('ops-asset').innerHTML = '<option value="">No specific asset</option>' + data.items.map(a => '<option value="' + a.id + '">' + WW.escape(a.name) + ' (' + WW.escape(a.assetCode) + ')</option>').join('');
    } catch { }
  }

  async function refresh() {
    await Promise.all([loadStats(), loadOrders()]);
  }

  async function loadStats() {
    try {
      const stats = await WW.get('/api/work-orders/statistics');
      document.getElementById('ops-total').textContent = WW.fmtInt(stats.total);
      document.getElementById('ops-open').textContent = WW.fmtInt(stats.open);
      document.getElementById('ops-overdue').textContent = WW.fmtInt(stats.overdue);
      document.getElementById('ops-verify').textContent = WW.fmtInt(stats.byStatus.find(s => s.label === 'Resolved')?.count || 0);
      document.getElementById('ops-availability').innerHTML = '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>' + WW.escape(stats.availabilityMessage) + '</div></div>';
      if (stats.total) {
        WW.chart('ops-chart', {
          type: 'bar',
          data: { labels: stats.byStatus.map(s => s.label), datasets: [{ data: stats.byStatus.map(s => s.count), backgroundColor: '#1d6fb8', borderRadius: 4 }] },
          options: { responsive: true, maintainAspectRatio: false, indexAxis: 'y', plugins: { legend: { display: false } }, scales: { x: { ...WW.axis(''), ticks: { precision: 0 } }, y: WW.axis('') } }
        });
      }
    } catch (e) { WW.toast(e.message, 'error', 'Work order statistics'); }
  }

  async function loadOrders() {
    const params = new URLSearchParams({ page: '1', pageSize: '100' });
    const status = document.getElementById('ops-status').value;
    const priority = document.getElementById('ops-priority').value;
    const type = document.getElementById('ops-type').value;
    if (status) params.set('status', status);
    if (priority) params.set('priority', priority);
    if (type) params.set('type', type);
    try {
      const data = await WW.get('/api/work-orders?' + params.toString());
      document.getElementById('ops-count').textContent = data.total + ' work orders';
      document.getElementById('ops-table').innerHTML = data.items.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Reference / title</th><th>Type</th><th>Priority</th><th>Status</th><th>Assigned</th><th>Due</th></tr></thead><tbody>' +
          data.items.map(w => '<tr class="clickable" data-order="' + w.id + '"><td><strong>' + WW.escape(w.reference) + '</strong><div class="tiny muted">' + WW.escape(w.title) + '</div></td><td>' + WW.escape(w.type) + '</td><td>' + WW.status(w.priority) + '</td><td>' + WW.status(w.status) + '</td><td>' + WW.escape(w.assignedTo || '—') + '</td><td class="' + (w.isOverdue ? 'text-danger' : '') + '">' + WW.fmtDate(w.dueAt) + (w.isOverdue ? ' <span class="badge badge-red">Overdue</span>' : '') + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No work orders', 'Adjust the filters or create a work order.', 'fa-clipboard');
      document.querySelectorAll('[data-order]').forEach(row => row.addEventListener('click', () => openOrder(row.dataset.order)));
    } catch (e) { document.getElementById('ops-table').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  async function openOrder(id) {
    try {
      const detail = await WW.get('/api/work-orders/' + id);
      currentOrder = detail.workOrder;
      const w = detail.workOrder;
      document.getElementById('ops-detail-body').innerHTML =
        '<div class="flex-between flex-wrap mb-2"><div><div class="inspector-title">' + WW.escape(w.reference) + '</div><div class="inspector-sub">' + WW.escape(w.title) + ' · ' + WW.status(w.status) + ' ' + WW.status(w.priority) + '</div></div>' +
        (canManage && transitions[w.status]?.length ? '<button class="btn btn-primary btn-sm" id="open-transition"><i class="fa fa-exchange"></i> Update status</button>' : '') + '</div>' +
        '<div class="grid grid-3 mb-2"><div class="callout"><div class="label">Type</div><div class="value" style="font-size:13px">' + WW.escape(w.type) + '</div></div><div class="callout"><div class="label">Assigned to</div><div class="value" style="font-size:13px">' + WW.escape(w.assignedTo || 'Unassigned') + '</div></div><div class="callout"><div class="label">Due</div><div class="value" style="font-size:13px">' + WW.fmtDate(w.dueAt) + '</div></div></div>' +
        (w.description ? '<div class="small mb-2">' + WW.escape(w.description) + '</div>' : '') +
        '<div class="kv"><dt>Asset</dt><dd>' + WW.escape(w.assetName || '—') + '</dd></div>' +
        '<div class="kv"><dt>Reported by</dt><dd>' + WW.escape(w.reportedBy || '—') + '</dd></div>' +
        '<div class="kv"><dt>Resolution</dt><dd>' + WW.escape(w.resolutionNotes || '—') + ' ' + (w.resolvedBy ? '<span class="muted">(' + WW.escape(w.resolvedBy) + ')</span>' : '') + '</dd></div>' +
        '<div class="kv"><dt>Verification</dt><dd>' + WW.escape(w.verificationNotes || '—') + ' ' + (w.verifiedBy ? '<span class="muted">(' + WW.escape(w.verifiedBy) + ')</span>' : '') + '</dd></div>' +
        '<h3 class="mt-2" style="font-size:13px">Timeline</h3><div class="timeline">' +
        detail.events.map(e => '<div class="timeline-item"><div class="when">' + WW.fmtDate(e.changedAt, true) + ' · ' + WW.escape(e.changedBy) + '</div><strong>' + (e.fromStatus ? WW.escape(e.fromStatus) + ' → ' : '') + WW.escape(e.toStatus) + '</strong>' + (e.notes ? '<div class="small muted">' + WW.escape(e.notes) + '</div>' : '') + '</div>').join('') + '</div>';
      document.getElementById('open-transition')?.addEventListener('click', openTransition);
      WW.modal.open('ops-detail-modal');
    } catch (e) { WW.toast(e.message, 'error', 'Work order'); }
  }

  function openTransition() {
    if (!currentOrder) return;
    const options = transitions[currentOrder.status] || [];
    document.getElementById('transition-status').innerHTML = options.map(s => '<option>' + s + '</option>').join('');
    document.getElementById('ops-transition-form').reset();
    document.getElementById('transition-status').innerHTML = options.map(s => '<option>' + s + '</option>').join('');
    WW.modal.close('ops-detail-modal');
    WW.modal.open('ops-transition-modal');
  }

  async function submitTransition(e) {
    e.preventDefault();
    if (!currentOrder) return;
    const data = new FormData(e.currentTarget);
    try {
      await WW.post('/api/work-orders/' + currentOrder.id + '/transition', {
        status: data.get('status'), notes: data.get('notes') || null,
        assignedTo: data.get('assignedTo') || null
      });
      WW.modal.close('ops-transition-modal');
      WW.toast('Work order updated.', 'warn', 'Saved');
      await refresh();
      await openOrder(currentOrder.id);
    } catch (ex) { WW.toast(ex.message, 'error', 'Status transition'); }
  }

  async function createOrder(e) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    try {
      await WW.post('/api/work-orders', {
        title: data.get('title'), description: data.get('description') || null, type: data.get('type'),
        priority: data.get('priority'), assetId: data.get('assetId') || null,
        assignedTo: data.get('assignedTo') || null,
        dueAt: data.get('dueAt') ? new Date(data.get('dueAt')).toISOString() : null,
        latitude: data.get('latitude') ? Number(data.get('latitude')) : null,
        longitude: data.get('longitude') ? Number(data.get('longitude')) : null
      });
      WW.modal.close('ops-create-modal'); WW.toast('Work order created.', 'warn', 'Saved'); await refresh();
    } catch (ex) { WW.toast(ex.message, 'error', 'Create work order'); }
  }
})();
