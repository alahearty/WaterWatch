(function () {
  'use strict';
  let users = [];
  let roles = [];
  let editingUser = null;

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    bindTabs();
    bindForms();
    await Promise.all([loadUsers(), loadBatches(), loadAudit(), loadSources()]);
  }

  function bindTabs() {
    document.querySelectorAll('#admin-tabs button').forEach(btn => btn.addEventListener('click', () => {
      document.querySelectorAll('#admin-tabs button').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.toggle('active', p.id === btn.dataset.tab));
    }));
  }

  function bindForms() {
    document.getElementById('roles-form')?.addEventListener('submit', async e => {
      e.preventDefault();
      if (!editingUser) return;
      const selected = Array.from(document.querySelectorAll('#roles-checkboxes input:checked')).map(i => i.value);
      try {
        await WW.put('/api/admin/users/' + editingUser.id + '/roles', { roles: selected });
        WW.modal.close('roles-modal'); WW.toast('Roles updated.', 'warn', 'Saved'); await loadUsers();
      } catch (ex) { WW.toast(ex.message, 'error', 'Roles'); }
    });
    document.getElementById('spatial-import-form')?.addEventListener('submit', async e => {
      e.preventDefault();
      const form = e.currentTarget;
      const fd = new FormData(form);
      fd.set('commit', form.querySelector('[name=commit]').checked ? 'true' : 'false');
      const result = document.getElementById('spatial-import-result');
      result.innerHTML = '<div class="alert alert-info"><i class="fa fa-spinner fa-spin"></i><div>Parsing, validating and checking duplicates…</div></div>';
      try {
        const response = await WW.post('/api/import', fd);
        result.innerHTML = '<div class="alert alert-' + (response.rowsRejected > 0 ? 'warn' : 'success') + '"><i class="fa fa-info-circle"></i><div><strong>' + WW.escape(response.status) + '</strong> · ' + response.rowsAccepted + ' accepted, ' + response.rowsRejected + ' rejected, ' + response.duplicates + ' duplicates in layer ' + WW.escape(response.targetLayerKey) + '.</div></div>' +
          (response.errors.length ? '<div class="table-wrap mt-2"><table class="ww-table"><thead><tr><th>Row</th><th>Error</th></tr></thead><tbody>' + response.errors.slice(0, 20).map(x => '<tr><td>' + x.row + '</td><td>' + WW.escape(x.message) + '</td></tr>').join('') + '</tbody></table></div>' : '') +
          (response.committed ? '<div class="availability mt-2"><i class="fa fa-check"></i><div>Committed. Reload GIS Explorer to see the layer.</div></div>' : '');
        if (response.committed) await loadBatches();
      } catch (ex) { result.innerHTML = '<div class="alert alert-danger">' + WW.escape(ex.message) + '</div>'; }
    });
    document.getElementById('service-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/import/service-layer', Object.fromEntries(data.entries()));
        document.getElementById('service-result').innerHTML = '<div class="alert alert-success">Service layer registered.</div>';
        e.currentTarget.reset();
      } catch (ex) { document.getElementById('service-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(ex.message) + '</div>'; }
    });
    document.getElementById('raster-form')?.addEventListener('submit', async e => {
      e.preventDefault(); const data = new FormData(e.currentTarget);
      const body = Object.fromEntries(data.entries());
      ['srid', 'resolutionMeters', 'minLon', 'minLat', 'maxLon', 'maxLat'].forEach(k => { body[k] = body[k] === '' ? null : Number(body[k]); });
      try {
        await WW.post('/api/import/raster-metadata', body);
        document.getElementById('raster-result').innerHTML = '<div class="alert alert-success">Raster metadata registered. Raster tile processing is not enabled.</div>';
        e.currentTarget.reset();
      } catch (ex) { document.getElementById('raster-result').innerHTML = '<div class="alert alert-danger">' + WW.escape(ex.message) + '</div>'; }
    });
  }

  async function loadUsers() {
    try {
      const [u, r] = await Promise.all([WW.get('/api/admin/users'), WW.get('/api/admin/roles')]);
      users = u; roles = r;
      document.getElementById('user-count').textContent = users.length + ' users';
      document.getElementById('admin-roles').innerHTML = roles.map(x => '<span class="badge badge-outline" style="margin:2px">' + WW.escape(x) + '</span>').join('');
      document.getElementById('admin-users-table').innerHTML = '<div class="table-wrap"><table class="ww-table"><thead><tr><th>User</th><th>Organisation</th><th>Roles</th><th>Status</th><th></th></tr></thead><tbody>' +
        users.map(u => '<tr><td><strong>' + WW.escape(u.displayName) + '</strong><div class="tiny muted">' + WW.escape(u.email) + '</div></td><td>' + WW.escape(u.organisation || '—') + '</td><td>' + u.roles.map(r => '<span class="badge badge-teal" style="margin:1px">' + WW.escape(r) + '</span>').join('') + '</td><td>' + (u.isActive ? '<span class="badge badge-green">Active</span>' : '<span class="badge badge-grey">Disabled</span>') + '</td><td class="actions"><button class="btn btn-sm" data-edit-roles="' + u.id + '">Edit roles</button></td></tr>').join('') + '</tbody></table></div>';
      document.querySelectorAll('[data-edit-roles]').forEach(btn => btn.addEventListener('click', () => openRoles(users.find(u => u.id === btn.dataset.editRoles))));
    } catch (e) { document.getElementById('admin-users-table').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }

  function openRoles(user) {
    editingUser = user;
    document.getElementById('roles-user').value = user.displayName + ' · ' + user.email;
    document.getElementById('roles-checkboxes').innerHTML = roles.map(r => '<label class="checkbox"><input type="checkbox" value="' + WW.escape(r) + '" ' + (user.roles.includes(r) ? 'checked' : '') + ' /> ' + WW.escape(r) + '</label>').join('');
    WW.modal.open('roles-modal');
  }

  async function loadBatches() {
    try {
      const data = await WW.get('/api/admin/imports');
      document.getElementById('admin-batches-table').innerHTML = data.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>File</th><th>Layer</th><th>Format</th><th>Status</th><th class="num">Accepted</th><th class="num">Rejected</th><th>Created</th></tr></thead><tbody>' +
          data.map(b => '<tr><td>' + WW.escape(b.fileName) + '</td><td>' + WW.escape(b.targetLayerKey || '—') + '</td><td>' + WW.escape(b.format || '—') + '</td><td>' + WW.status(b.status) + '</td><td class="num">' + b.rowsAccepted + '</td><td class="num">' + b.rowsRejected + '</td><td>' + WW.fmtDate(b.createdAt, true) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No imports', 'Spatial import batches will be listed here.', 'fa-upload');
    } catch { }
  }

  async function loadAudit() {
    try {
      const data = await WW.get('/api/admin/audit?limit=200');
      document.getElementById('admin-audit-table').innerHTML = data.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>When</th><th>User</th><th>Action</th><th>Entity</th><th>Changes</th></tr></thead><tbody>' +
          data.map(a => '<tr><td class="nowrap">' + WW.fmtDate(a.occurredAt, true) + '</td><td>' + WW.escape(a.userName || 'system') + '</td><td>' + WW.escape(a.action) + '</td><td>' + WW.escape(a.entityType) + '<div class="tiny muted">' + WW.escape(a.entityId || '') + '</div></td><td class="small mono">' + WW.escape(a.detailsJson || '') + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No audit entries', 'Writes will be recorded here.', 'fa-history');
    } catch { }
  }

  async function loadSources() {
    try {
      const data = await WW.get('/api/geography/sources');
      document.getElementById('admin-sources-table').innerHTML = '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Source</th><th>Publisher</th><th>Licence</th><th>Acquired</th><th>Official</th><th>Quality note</th></tr></thead><tbody>' +
        data.map(s => '<tr><td><strong>' + WW.escape(s.name) + '</strong><div class="tiny muted">' + WW.escape(s.sourceUrl || '') + '</div></td><td>' + WW.escape(s.publisher) + '</td><td>' + WW.escape(s.licence || '—') + '</td><td>' + WW.fmtDate(s.acquiredAt) + '</td><td>' + (s.isOfficial ? '<span class="badge badge-green">Official</span>' : '<span class="badge badge-grey">Not official</span>') + '</td><td class="small">' + WW.escape(s.qualityNotes || '') + '</td></tr>').join('') + '</tbody></table></div>';
    } catch { }
  }
})();
