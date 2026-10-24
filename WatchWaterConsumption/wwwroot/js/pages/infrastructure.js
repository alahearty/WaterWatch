(function () {
  'use strict';
  const canEdit = document.body.dataset.authenticated === 'true' && document.getElementById('open-asset-create') !== null;
  const map = L.map('inf-map', { zoomControl: true }).setView([4.8156, 7.0498], 12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors', maxZoom: 19
  }).addTo(map);
  const adminLayer = L.geoJSON(null, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.03 } }).addTo(map);
  const assetLayer = L.layerGroup().addTo(map);
  let page = 1;
  const pageSize = 25;
  let total = 0;
  let searchTimer = null;

  async function init() {
    populateTypes();
    await Promise.all([loadSelectors(), loadStatistics(), loadAssets()]);
    await loadMap();
    document.getElementById('inf-search').addEventListener('input', () => {
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => { page = 1; loadAssets(); }, 260);
    });
    ['inf-type', 'inf-status'].forEach(id => document.getElementById(id).addEventListener('change', () => { page = 1; loadAssets(); }));
    document.getElementById('inf-refresh').addEventListener('click', () => { loadAssets(); loadStatistics(); });
    const open = document.getElementById('open-asset-create');
    if (open) open.addEventListener('click', () => WW.modal.open('asset-editor'));
    const form = document.getElementById('asset-editor-form');
    if (form) form.addEventListener('submit', submitAsset);
  }

  function populateTypes() {
    const types = ['Borehole', 'Well', 'TreatmentPlant', 'Reservoir', 'ElevatedTank', 'GroundStorageTank',
      'PumpingStation', 'DistributionMain', 'TransmissionMain', 'Valve', 'Hydrant', 'WaterMeter',
      'PressureMonitoringPoint', 'SamplingStation', 'DrainageAsset', 'Other'];
    document.getElementById('inf-type').innerHTML = '<option value="">All types</option>' + types.map(t => '<option>' + t + '</option>').join('');
    const select = document.querySelector('#asset-editor-form select[name=assetType]');
    if (select) select.innerHTML = types.filter(t => t !== 'DistributionMain' && t !== 'TransmissionMain').map(t => '<option>' + t + '</option>').join('');
    const hint = document.querySelector('#asset-editor-form .alert-warn div');
    if (hint) hint.textContent += ' Pipeline assets must be created from a drawn line in GIS Explorer.';
  }

  async function loadSelectors() {
    try {
      const [lgas, zones] = await Promise.all([
        WW.get('/api/geography/admin-areas?level=Lga'),
        WW.get('/api/geography/operational-areas?type=ServiceZone')
      ]);
      lgas.forEach(l => {
        const o = document.createElement('option'); o.value = l.id; o.textContent = l.name + ' LGA';
        document.getElementById('new-admin')?.appendChild(o);
      });
      zones.forEach(z => {
        const o = document.createElement('option'); o.value = z.id; o.textContent = z.name;
        document.getElementById('new-zone')?.appendChild(o);
      });
    } catch { }
  }

  async function loadStatistics() {
    try {
      const stats = await WW.get('/api/assets/statistics');
      document.getElementById('inf-total').textContent = WW.fmtInt(stats.total);
      document.getElementById('inf-operational').textContent = WW.fmtInt(stats.byStatus.find(s => s.label === 'Operational')?.count || 0);
      document.getElementById('inf-poor').textContent = WW.fmtInt((stats.byCondition.find(s => s.label === 'Poor')?.count || 0) + (stats.byCondition.find(s => s.label === 'Critical')?.count || 0));
      document.getElementById('inf-nogeom').textContent = WW.fmtInt(stats.withoutGeometry);
    } catch { }
  }

  async function loadAssets() {
    const target = document.getElementById('inf-table');
    target.innerHTML = '<div class="loading-block"><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div></div>';
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    const search = document.getElementById('inf-search').value.trim();
    const type = document.getElementById('inf-type').value;
    const status = document.getElementById('inf-status').value;
    if (search) params.set('search', search);
    if (type) params.set('type', type);
    if (status) params.set('status', status);
    try {
      const result = await WW.get('/api/assets?' + params.toString());
      total = result.total;
      document.getElementById('inf-count').textContent = result.total + ' assets';
      if (!result.items.length) {
        target.innerHTML = WW.empty('No matching assets', 'Adjust the filters or register a new asset.', 'fa-wrench');
      } else {
        target.innerHTML = '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Asset</th><th>Type</th><th>Status</th><th>Condition</th><th>Area / zone</th><th>Class</th></tr></thead><tbody>' +
          result.items.map(a => '<tr class="clickable" data-href="/Infrastructure/Details/' + encodeURIComponent(a.id) + '">' +
            '<td><strong>' + WW.escape(a.name) + '</strong><div class="tiny muted">' + WW.escape(a.assetCode) + (a.isSensitive ? ' · <i class="fa fa-lock"></i> sensitive' : '') + '</div></td>' +
            '<td>' + WW.escape(a.assetType) + '</td><td>' + WW.status(a.status) + '</td><td>' + WW.status(a.condition) + '</td>' +
            '<td class="small">' + WW.escape(a.adminAreaName || '—') + '<div class="tiny muted">' + WW.escape(a.operationalAreaName || '—') + '</div></td>' +
            '<td>' + WW.classification(a.dataClassification) + '</td></tr>').join('') + '</tbody></table></div>';
        target.querySelectorAll('tr[data-href]').forEach(row => row.addEventListener('click', () => { window.location.href = row.dataset.href; }));
      }
      renderPagination();
    } catch (e) {
      target.innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>';
    }
  }

  function renderPagination() {
    const pages = Math.max(1, Math.ceil(total / pageSize));
    document.getElementById('inf-pagination').innerHTML =
      '<span>Page ' + page + ' of ' + pages + ' · ' + total + ' records</span>' +
      '<span class="pages"><button class="btn btn-sm" id="pg-prev" ' + (page <= 1 ? 'disabled' : '') + '><i class="fa fa-angle-left"></i></button>' +
      '<button class="btn btn-sm" id="pg-next" ' + (page >= pages ? 'disabled' : '') + '><i class="fa fa-angle-right"></i></button></span>';
    document.getElementById('pg-prev')?.addEventListener('click', () => { if (page > 1) { page--; loadAssets(); } });
    document.getElementById('pg-next')?.addEventListener('click', () => { if (page < pages) { page++; loadAssets(); } });
  }

  async function loadMap() {
    try {
      const ctx = await WW.get('/api/geography/context');
      if (ctx.extent) map.fitBounds([[ctx.extent.minLat, ctx.extent.minLon], [ctx.extent.maxLat, ctx.extent.maxLon]], { padding: [16, 16] });
      const admin = await WW.get('/api/layers/admin-lga/geojson');
      adminLayer.addData(admin);
      const assets = await WW.get('/api/layers/assets/geojson?limit=2000');
      assetLayer.clearLayers();
      L.geoJSON(assets, {
        pointToLayer: (f, latlng) => L.circleMarker(latlng, { radius: 5, color: '#fff', weight: 1.4, fillColor: '#0284c7', fillOpacity: 0.9 }),
        onEachFeature: (f, layer) => layer.bindPopup(WW.popupHtml(f))
      }).addTo(assetLayer);
    } catch { }
  }

  async function submitAsset(e) {
    e.preventDefault();
    const form = e.currentTarget;
    const data = new FormData(form);
    const body = {
      assetCode: data.get('assetCode'), name: data.get('name'), assetType: data.get('assetType'),
      description: data.get('description') || null, latitude: Number(data.get('latitude')), longitude: Number(data.get('longitude')),
      adminAreaId: data.get('adminAreaId') || null, operationalAreaId: data.get('operationalAreaId') || null,
      owner: data.get('owner') || null, operator: data.get('operator') || null,
      installedOn: data.get('installedOn') || null, capacityM3PerDay: data.get('capacityM3PerDay') ? Number(data.get('capacityM3PerDay')) : null,
      status: data.get('status'), condition: data.get('condition'),
      specificationsJson: data.get('specificationsJson') || null, sourceReference: data.get('sourceReference') || null,
      isSensitive: data.get('isSensitive') === 'on'
    };
    try {
      const created = await WW.post('/api/assets', body);
      WW.modal.close('asset-editor');
      form.reset();
      WW.toast('Asset ' + created.assetCode + ' created.', 'warn', 'Saved');
      await Promise.all([loadAssets(), loadStatistics(), loadMap()]);
    } catch (ex) { WW.toast(ex.message, 'error', 'Create asset'); }
  }

  init();
})();
