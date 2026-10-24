(function () {
  'use strict';
  const root = document.getElementById('asset-detail-root');
  const assetId = root.dataset.assetId;
  const canEdit = !!document.getElementById('asset-edit-button');
  const canField = !!document.getElementById('add-inspection');
  let detail = null;
  const map = L.map('asset-map', { zoomControl: true }).setView([4.8156, 7.0498], 13);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
  L.geoJSON(null, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0.03 } }).addTo(map);
  const assetLayer = L.layerGroup().addTo(map);

  async function init() {
    try {
      detail = await WW.get('/api/assets/' + assetId);
      render();
      setupForms();
    } catch (e) {
      document.getElementById('asset-name').textContent = 'Asset unavailable';
      document.getElementById('asset-subtitle').textContent = e.message;
      document.getElementById('asset-facts').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>';
      document.getElementById('asset-availability').innerHTML = '<div class="alert alert-warn"><i class="fa fa-lock"></i><div>This asset may be sensitive or may not exist. Sign in with an authorised role to view restricted infrastructure.</div></div>';
    }
  }

  function render() {
    document.title = detail.name + ' · WaterWatch';
    document.getElementById('asset-name').textContent = detail.name;
    document.getElementById('asset-subtitle').innerHTML = WW.escape(detail.assetCode) + ' · ' + WW.escape(detail.assetType) + ' · ' + WW.status(detail.status) + ' ' + WW.classification(detail.dataClassification);
    document.getElementById('asset-class').innerHTML = WW.classification(detail.dataClassification) + (detail.isSensitive ? ' <span class="badge badge-red"><i class="fa fa-lock"></i> Sensitive</span>' : '');
    document.getElementById('asset-availability').innerHTML = detail.dataClassification === 'SyntheticDemonstration'
      ? '<div class="alert alert-warn"><i class="fa fa-flask"></i><div>This asset is synthetic demonstration data. It is not a real Port Harcourt utility asset.</div></div>'
      : '';
    const facts = [
      ['Asset code', detail.assetCode], ['Type', detail.assetType], ['Status', detail.status],
      ['Condition', detail.condition], ['Owner', detail.owner], ['Operator', detail.operator],
      ['Installed', detail.installedOn || '—'], ['Capacity', detail.capacityM3PerDay ? WW.fmt(detail.capacityM3PerDay, 2) + ' m³/day' : '—'],
      ['Admin area', detail.adminAreaName || '—'], ['Service zone', detail.operationalAreaName || '—'],
      ['Longitude', detail.longitude != null ? detail.longitude.toFixed(6) : '—'],
      ['Latitude', detail.latitude != null ? detail.latitude.toFixed(6) : '—'],
      ['Created', WW.fmtDate(detail.createdAt, true)], ['Last updated', WW.fmtDate(detail.updatedAt, true)]
    ];
    document.getElementById('asset-facts').innerHTML = facts.map(f => '<div class="kv"><dt>' + WW.escape(f[0]) + '</dt><dd>' + (f[0] === 'Status' || f[0] === 'Condition' ? WW.status(f[1]) : WW.escape(f[1])) + '</dd></div>').join('') +
      (detail.description ? '<div class="mt-2 small">' + WW.escape(detail.description) + '</div>' : '');
    document.getElementById('asset-specs').textContent = pretty(detail.specificationsJson) || 'No technical specifications recorded.';
    if (detail.designParametersJson) {
      document.getElementById('asset-specs').textContent += '\n\nDesign parameters:\n' + pretty(detail.designParametersJson);
    }
    document.getElementById('asset-source').innerHTML = detail.source
      ? '<div class="kv"><dt>Source</dt><dd><a href="' + WW.escape(detail.source.sourceUrl || '#') + '" target="_blank" rel="noopener">' + WW.escape(detail.source.name) + '</a></dd>' +
        '<dt>Licence</dt><dd>' + WW.escape(detail.source.licence || 'Not recorded') + '</dd><dt>Reference</dt><dd>' + WW.escape(detail.sourceReference || '—') + '</dd></div>'
      : '<div class="availability"><i class="fa fa-info-circle"></i><div>No external source is recorded. The asset is an operator-entered registry record.</div></div>';
    renderLists();
    renderMap();
    populateEditForm();
  }

  function renderLists() {
    document.getElementById('asset-inspections').innerHTML = detail.inspections.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Scheduled</th><th>Completed</th><th>Inspector</th><th>Condition</th></tr></thead><tbody>' +
        detail.inspections.map(i => '<tr><td>' + WW.fmtDate(i.scheduledFor) + '</td><td>' + WW.fmtDate(i.completedAt) + '</td><td>' + WW.escape(i.inspectorName || '—') + '</td><td>' + WW.status(i.conditionAfter) + '</td></tr>').join('') + '</tbody></table></div>'
      : '<div class="table-empty"><i class="fa fa-search"></i>No inspections recorded.</div>';
    document.getElementById('asset-maintenance').innerHTML = detail.maintenanceRecords.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Date</th><th>Type</th><th>Performed by</th><th>Description</th></tr></thead><tbody>' +
        detail.maintenanceRecords.map(m => '<tr><td>' + WW.fmtDate(m.performedAt) + '</td><td>' + WW.escape(m.type) + '</td><td>' + WW.escape(m.performedBy || '—') + '</td><td>' + WW.escape(m.description || '—') + '</td></tr>').join('') + '</tbody></table></div>'
      : '<div class="table-empty"><i class="fa fa-wrench"></i>No maintenance records.</div>';
    document.getElementById('asset-documents').innerHTML = detail.documents.length
      ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>File</th><th>Type</th><th>Size</th><th>Caption</th></tr></thead><tbody>' +
        detail.documents.map(d => '<tr><td>' + WW.escape(d.fileName) + '</td><td>' + WW.escape(d.contentType) + '</td><td>' + WW.fmtInt(d.sizeBytes) + '</td><td>' + WW.escape(d.caption || '—') + '</td></tr>').join('') + '</tbody></table></div>'
      : '<div class="table-empty"><i class="fa fa-file-o"></i>No documents or photographs attached.</div>';
  }

  function renderMap() {
    if (detail.geometryGeoJson && detail.geometryGeoJson !== 'null') {
      try {
        assetLayer.clearLayers();
        const geometry = JSON.parse(detail.geometryGeoJson);
        L.geoJSON(geometry, {
          style: { color: '#0f8b8d', weight: 2, fillOpacity: 0.15 },
          pointToLayer: (f, latlng) => L.circleMarker(latlng, { radius: 8, color: '#fff', weight: 2, fillColor: '#0f8b8d', fillOpacity: 0.95 })
        }).addTo(assetLayer).bindPopup(WW.popupHtml({ properties: { name: detail.name, assetCode: detail.assetCode, assetType: detail.assetType, status: detail.status } }));
        const bounds = assetLayer.getBounds();
        if (bounds.isValid()) map.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 });
      } catch { }
    }
    WW.get('/api/layers/admin-lga/geojson').then(data => {
      L.geoJSON(data, { style: { color: '#0ea5e9', weight: 2, fillOpacity: 0 } }).addTo(map);
    }).catch(() => { });
  }

  function populateEditForm() {
    if (!canEdit) return;
    const form = document.getElementById('asset-edit-form');
    const types = ['Borehole', 'Well', 'TreatmentPlant', 'Reservoir', 'ElevatedTank', 'GroundStorageTank',
      'PumpingStation', 'DistributionMain', 'TransmissionMain', 'Valve', 'Hydrant', 'WaterMeter',
      'PressureMonitoringPoint', 'SamplingStation', 'DrainageAsset', 'Other'];
    form.querySelector('[name=assetType]').innerHTML = types.map(t => '<option>' + t + '</option>').join('');
    form.querySelector('[name=assetCode]').value = detail.assetCode;
    form.querySelector('[name=name]').value = detail.name;
    form.querySelector('[name=assetType]').value = detail.assetType;
    form.querySelector('[name=status]').value = detail.status;
    form.querySelector('[name=condition]').value = detail.condition;
    form.querySelector('[name=capacityM3PerDay]').value = detail.capacityM3PerDay ?? '';
    form.querySelector('[name=description]').value = detail.description || '';
    form.querySelector('[name=owner]').value = detail.owner || '';
    form.querySelector('[name=operator]').value = detail.operator || '';
    form.querySelector('[name=installedOn]').value = detail.installedOn || '';
    form.querySelector('[name=sourceReference]').value = detail.sourceReference || '';
    form.querySelector('[name=specificationsJson]').value = detail.specificationsJson || '';
    form.querySelector('[name=designParametersJson]').value = detail.designParametersJson || '';
    form.querySelector('[name=isSensitive]').checked = detail.isSensitive;
  }

  function setupForms() {
    document.getElementById('asset-edit-button')?.addEventListener('click', () => WW.modal.open('asset-edit-modal'));
    document.getElementById('asset-edit-form')?.addEventListener('submit', async e => {
      e.preventDefault();
      const form = e.currentTarget;
      const data = new FormData(form);
      const body = {
        assetCode: data.get('assetCode'), name: data.get('name'), assetType: data.get('assetType'),
        description: data.get('description') || null, status: data.get('status'), condition: data.get('condition'),
        adminAreaId: detail.adminAreaId, operationalAreaId: detail.operationalAreaId,
        owner: data.get('owner') || null, operator: data.get('operator') || null,
        installedOn: data.get('installedOn') || null,
        capacityM3PerDay: data.get('capacityM3PerDay') ? Number(data.get('capacityM3PerDay')) : null,
        specificationsJson: data.get('specificationsJson') || null,
        designParametersJson: data.get('designParametersJson') || null,
        isSensitive: data.get('isSensitive') === 'on', sourceReference: data.get('sourceReference') || null,
        geometryGeoJson: detail.geometryGeoJson, latitude: detail.latitude, longitude: detail.longitude
      };
      try {
        detail = await WW.put('/api/assets/' + assetId, body);
        WW.modal.close('asset-edit-modal');
        WW.toast('Asset updated.', 'warn', 'Saved');
        render();
      } catch (ex) { WW.toast(ex.message, 'error', 'Update asset'); }
    });

    document.getElementById('add-inspection')?.addEventListener('click', () => WW.modal.open('inspection-modal'));
    document.getElementById('inspection-form')?.addEventListener('submit', async e => {
      e.preventDefault();
      const data = new FormData(e.currentTarget);
      const scheduled = data.get('scheduledFor') ? new Date(data.get('scheduledFor')).toISOString() : new Date().toISOString();
      try {
        await WW.post('/api/assets/' + assetId + '/inspections', {
          scheduledFor: scheduled,
          completedAt: data.get('completedAt') ? new Date(data.get('completedAt')).toISOString() : null,
          inspectorName: data.get('inspectorName') || null,
          findings: data.get('findings') || null,
          conditionAfter: data.get('conditionAfter')
        });
        WW.modal.close('inspection-modal');
        WW.toast('Inspection recorded.', 'warn', 'Saved');
        detail = await WW.get('/api/assets/' + assetId); render();
      } catch (ex) { WW.toast(ex.message, 'error', 'Inspection'); }
    });

    document.getElementById('add-maintenance')?.addEventListener('click', () => WW.modal.open('maintenance-modal'));
    document.getElementById('maintenance-form')?.addEventListener('submit', async e => {
      e.preventDefault();
      const data = new FormData(e.currentTarget);
      try {
        await WW.post('/api/assets/' + assetId + '/maintenance', {
          type: data.get('type'),
          performedAt: data.get('performedAt') ? new Date(data.get('performedAt')).toISOString() : new Date().toISOString(),
          performedBy: data.get('performedBy') || null,
          description: data.get('description') || null,
          cost: data.get('cost') ? Number(data.get('cost')) : null
        });
        WW.modal.close('maintenance-modal');
        WW.toast('Maintenance record added.', 'warn', 'Saved');
        detail = await WW.get('/api/assets/' + assetId); render();
      } catch (ex) { WW.toast(ex.message, 'error', 'Maintenance'); }
    });
  }

  function pretty(json) {
    if (!json) return '';
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }

  init();
})();
