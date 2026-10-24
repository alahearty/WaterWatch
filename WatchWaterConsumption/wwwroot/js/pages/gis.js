(function () {
  'use strict';

  const layout = document.getElementById('gis-layout');
  const canEdit = layout.dataset.canEdit === 'true';
  const map = L.map('gis-map', { zoomControl: false, preferCanvas: true });
  map.setView([4.8156, 7.0498], 12);
  L.control.zoom({ position: 'bottomright' }).addTo(map);
  L.control.scale({ position: 'bottomleft', imperial: false }).addTo(map);

  const baseLayers = {
    osm: L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19, attribution: '&copy; OpenStreetMap contributors'
    }),
    satellite: L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
      maxZoom: 19, attribution: 'Tiles &copy; Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community'
    }),
    topo: L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}', {
      maxZoom: 19, attribution: 'Tiles &copy; Esri'
    }),
    dark: L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
      maxZoom: 20, attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
    })
  };
  let activeBase = 'osm';
  baseLayers.osm.addTo(map);

  const drawnGroup = L.featureGroup().addTo(map);
  const analysisGroup = L.featureGroup().addTo(map);
  const selectionMarker = L.layerGroup().addTo(map);
  const entries = new Map();
  const order = [];
  let context = null;
  let selected = null;
  let activeTool = 'select';
  let currentDraw = null;
  let pendingEdit = null;
  let scratchCounter = 0;

  const el = {
    layerTree: document.getElementById('layer-tree'),
    inspector: document.getElementById('inspector'),
    scratch: document.getElementById('scratch-results'),
    legend: document.getElementById('map-legend'),
    coords: document.getElementById('coordinate-display'),
    hint: document.getElementById('draw-hint'),
    search: document.getElementById('map-search-input'),
    searchResults: document.getElementById('map-search-results'),
    assetModal: 'asset-modal',
    bufferModal: 'buffer-modal'
  };

  const assetTypes = ['Borehole', 'Well', 'TreatmentPlant', 'Reservoir', 'ElevatedTank', 'GroundStorageTank',
    'PumpingStation', 'DistributionMain', 'TransmissionMain', 'Valve', 'Hydrant', 'WaterMeter',
    'PressureMonitoringPoint', 'SamplingStation', 'DrainageAsset', 'Other'];

  function badge(value) { return WW.classification(value); }
  function number(value, digits) { return WW.fmt(value, digits); }
  function setHint(text) { el.hint.style.display = text ? 'block' : 'none'; el.hint.textContent = text || ''; }
  function setTool(buttonId) {
    document.querySelectorAll('.gis-toolstrip button').forEach(b => b.classList.remove('active'));
    const button = document.getElementById(buttonId);
    if (button) button.classList.add('active');
  }

  async function init() {
    await loadContext();
    await loadLayerCatalogue();
    bindTools();
    bindSearch();
    bindFilters();
    bindBookmarks();
    bindBasemaps();
    bindForms();
    map.on('mousemove', e => {
      el.coords.textContent = 'lat ' + e.latlng.lat.toFixed(5) + ' · lon ' + e.latlng.lng.toFixed(5);
    });
    map.on(L.Draw.Event.CREATED, onDrawCreated);
    map.on('draw:deleted', () => { });
    updateLegend();
    const pending = WW.mapTarget.take();
    if (pending && pending.lat && pending.lon) {
      map.setView([pending.lat, pending.lon], pending.zoom || 15);
      L.marker([pending.lat, pending.lon]).addTo(selectionMarker).bindPopup(WW.escape(pending.label || 'Selected location')).openPopup();
    }
    const term = sessionStorage.getItem('ww.mapSearchTerm');
    if (term) { el.search.value = term; sessionStorage.removeItem('ww.mapSearchTerm'); runSearch(term); }
  }

  async function loadContext() {
    try {
      context = await WW.get('/api/geography/context');
      if (context.extent) {
        map.fitBounds([[context.extent.minLat, context.extent.minLon], [context.extent.maxLat, context.extent.maxLon]], { padding: [20, 20] });
      }
      const adminSelect = document.getElementById('filter-admin');
      (context.lgas || []).forEach(lga => {
        const option = document.createElement('option');
        option.value = lga.id; option.textContent = lga.name + ' LGA';
        adminSelect.appendChild(option);
        const assetAdmin = document.getElementById('asset-admin');
        const option2 = option.cloneNode(true);
        assetAdmin.appendChild(option2);
      });
      const zones = await WW.get('/api/geography/operational-areas?type=ServiceZone');
      const zoneSelect = document.getElementById('asset-zone');
      zones.forEach(zone => {
        const option = document.createElement('option');
        option.value = zone.id; option.textContent = zone.name;
        zoneSelect.appendChild(option);
      });
    } catch (e) {
      WW.toast(e.message, 'error', 'Geography');
    }
  }

  function styleFor(key) {
    switch (key) {
      case 'admin-country': return { color: '#475569', weight: 1, fillOpacity: 0.02, dashArray: '5 4' };
      case 'admin-state': return { color: '#0f766e', weight: 1.6, fillOpacity: 0.02 };
      case 'admin-lga': return { color: '#0ea5e9', weight: 2, fillOpacity: 0.04 };
      case 'operational-zones': return { color: '#7c3aed', weight: 2, dashArray: '6 4', fillOpacity: 0.1 };
      case 'osm-waterways': return { color: '#0284c7', weight: 1.6, fillOpacity: 0 };
      case 'osm-water-bodies': return { color: '#0369a1', weight: 1, fillColor: '#7dd3fc', fillOpacity: 0.35 };
      case 'osm-settlements': return { color: '#334155', radius: 4, weight: 1, fillOpacity: 0.7 };
      case 'assets': return { color: '#0284c7', radius: 5, weight: 1.5, fillOpacity: 0.9 };
      case 'sampling-stations': return { color: '#be123c', radius: 6, weight: 1.5, fillOpacity: 0.9 };
      case 'hydrology-stations': return { color: '#0369a1', radius: 6, weight: 1.5, fillOpacity: 0.9 };
      case 'devices': return { color: '#ca8a04', radius: 5, weight: 1.5, fillOpacity: 0.9 };
      case 'flood-layers': return { color: '#1d4ed8', weight: 1.6, fillColor: '#3b82f6', fillOpacity: 0.22 };
      case 'work-orders': return { color: '#ea580c', radius: 5, weight: 1.5, fillOpacity: 0.9 };
      default: return { color: '#0f8b8d', weight: 1.8, fillColor: '#2dd4bf', fillOpacity: 0.15 };
    }
  }

  function pointStyleFor(key) {
    const base = styleFor(key);
    return { radius: base.radius || 5, color: '#ffffff', weight: 1.3, fillColor: base.color, fillOpacity: 0.95 };
  }

  async function loadLayerCatalogue() {
    try {
      const layers = await WW.get('/api/layers');
      el.layerTree.innerHTML = '';
      const groups = {};
      layers.forEach(layer => { (groups[layer.category] = groups[layer.category] || []).push(layer); });
      Object.keys(groups).sort().forEach(category => {
        const group = document.createElement('div');
        group.className = 'layer-group';
        group.innerHTML = '<div class="layer-group-title">' + WW.escape(category) + '</div>';
        groups[category].forEach(layer => group.appendChild(buildLayerItem(layer)));
        el.layerTree.appendChild(group);
      });
      if (!layers.length) el.layerTree.innerHTML = WW.empty('No layers', 'Import a spatial layer or check the server data sources.', 'fa-layer-group');
    } catch (e) {
      el.layerTree.innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>';
    }
  }

  function buildLayerItem(def) {
    const wrapper = document.createElement('div');
    wrapper.className = 'layer-item';
    wrapper.dataset.key = def.key;
    const countLabel = def.featureCount ? def.featureCount + ' features' : (def.serviceType || 'metadata');
    wrapper.innerHTML =
      '<div class="layer-item-head">' +
      '<input type="checkbox" aria-label="Toggle ' + WW.escape(def.name) + '" ' + (def.isVisibleByDefault ? 'checked' : '') + ' />' +
      '<span class="layer-item-name" title="' + WW.escape(def.name) + '">' + WW.escape(def.name) + '</span>' +
      '<span class="badge ' + (def.dataClassification === 'SyntheticDemonstration' ? 'badge-synthetic' : 'badge-outline') + '">' + WW.escape(shortClass(def.dataClassification)) + '</span>' +
      '</div>' +
      '<div class="tiny muted" style="margin:4px 0 0 22px">' + WW.escape(countLabel) + '</div>' +
      '<div class="layer-opacity"><input type="range" min="0" max="100" value="100" aria-label="Opacity" /><span>100%</span></div>' +
      '<div class="layer-tools" style="justify-content:flex-end;margin-top:4px">' +
      '<button type="button" data-act="up" title="Move up"><i class="fa fa-arrow-up"></i></button>' +
      '<button type="button" data-act="down" title="Move down"><i class="fa fa-arrow-down"></i></button>' +
      '<button type="button" data-act="fit" title="Fit to layer"><i class="fa fa-crosshairs"></i></button>' +
      '<button type="button" data-act="export" title="Export GeoJSON"><i class="fa fa-download"></i></button>' +
      '</div>';

    const entry = { def, layer: null, visible: def.isVisibleByDefault, opacity: 1, loaded: false, data: null };
    entries.set(def.key, entry);
    order.push(def.key);

    const checkbox = wrapper.querySelector('input[type=checkbox]');
    const slider = wrapper.querySelector('input[type=range]');
    checkbox.addEventListener('change', () => toggleLayer(entry, checkbox.checked));
    slider.addEventListener('input', () => {
      entry.opacity = Number(slider.value) / 100;
      wrapper.querySelector('.layer-opacity span').textContent = slider.value + '%';
      applyOpacity(entry);
    });
    wrapper.querySelectorAll('button[data-act]').forEach(button => {
      button.addEventListener('click', () => {
        const action = button.dataset.act;
        if (action === 'up') moveLayer(entry, -1);
        if (action === 'down') moveLayer(entry, 1);
        if (action === 'fit') fitLayer(entry);
        if (action === 'export') exportLayer(entry);
      });
    });
    setTimeout(() => { if (entry.visible) toggleLayer(entry, true); }, 0);
    return wrapper;
  }

  function shortClass(value) {
    return value === 'SyntheticDemonstration' ? 'SYNTH' : value === 'Imported' ? 'IMPORT' : value ? value.toUpperCase() : '—';
  }

  async function toggleLayer(entry, visible) {
    entry.visible = visible;
    if (!visible) {
      if (entry.layer) map.removeLayer(entry.layer);
      updateLegend();
      return;
    }
    if (entry.def.serviceType === 'WFS') {
      WW.toast('This layer is registered as a WFS service. WFS feature rendering is not enabled in this release; the service configuration is retained in Administration.', 'warn');
      return;
    }
    try {
      if (entry.def.serviceType === 'WMS') {
        if (!entry.layer) {
          entry.layer = L.tileLayer.wms(entry.def.serviceUrl, {
            layers: entry.def.serviceLayerName, format: 'image/png', transparent: true,
            attribution: entry.def.sourceName || '', opacity: entry.opacity
          });
        }
        entry.layer.addTo(map);
        updateLegend();
        return;
      }
      if (!entry.loaded) {
        const query = new URLSearchParams({ limit: '5000' });
        if (entry.def.key === 'assets') {
          const admin = document.getElementById('filter-admin').value;
          const type = document.getElementById('filter-type').value;
          const classification = document.getElementById('filter-class').value;
          if (admin) query.set('adminAreaId', admin);
          if (type) query.set('assetType', type);
          if (classification) query.set('classification', classification);
        }
        const data = await WW.get('/api/layers/' + encodeURIComponent(entry.def.key) + '/geojson?' + query.toString());
        entry.data = data;
        entry.layer = L.geoJSON(data, {
          style: feature => styleFor(entry.def.key),
          pointToLayer: (feature, latlng) => L.circleMarker(latlng, pointStyleFor(entry.def.key)),
          onEachFeature: (feature, layer) => {
            layer.on('click', () => selectFeature(entry.def.key, feature, layer));
          }
        });
        entry.loaded = true;
      }
      entry.layer.addTo(map);
      applyOpacity(entry);
      applyOrder();
      updateLegend();
    } catch (e) {
      WW.toast(e.message, 'error', 'Layer failed');
    }
  }

  function applyOpacity(entry) {
    if (!entry.layer) return;
    if (entry.layer.setOpacity) { entry.layer.setOpacity(entry.opacity); return; }
    if (entry.layer.eachLayer) {
      entry.layer.eachLayer(layer => {
        if (layer.setStyle) {
          layer.setStyle({ opacity: entry.opacity, fillOpacity: entry.opacity * (entry.def.key === 'flood-layers' ? 0.22 : entry.def.key === 'assets' ? 0.9 : 0.25) });
        }
      });
    }
  }

  function applyOrder() {
    order.forEach(key => {
      const entry = entries.get(key);
      if (entry && entry.layer && entry.visible && map.hasLayer(entry.layer)) {
        if (entry.layer.bringToFront) entry.layer.bringToFront();
        else if (entry.layer.setZIndex) entry.layer.setZIndex(order.indexOf(key) + 10);
      }
    });
  }

  function moveLayer(entry, direction) {
    const index = order.indexOf(entry.def.key);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= order.length) return;
    const temp = order[index]; order[index] = order[target]; order[target] = temp;
    const items = Array.from(el.layerTree.querySelectorAll('.layer-item'));
    const currentIndex = items.findIndex(x => x.dataset.key === entry.def.key);
    const otherIndex = items.findIndex(x => x.dataset.key === order[index]);
    if (currentIndex >= 0 && otherIndex >= 0) {
      const parent = items[currentIndex].parentNode;
      parent.insertBefore(items[currentIndex], direction < 0 ? items[otherIndex] : items[otherIndex].nextSibling);
    }
    applyOrder();
  }

  function fitLayer(entry) {
    if (!entry.layer) { WW.toast('Turn the layer on before fitting.', 'warn'); return; }
    try {
      const bounds = entry.layer.getBounds ? entry.layer.getBounds() : null;
      if (bounds && bounds.isValid()) map.fitBounds(bounds, { padding: [24, 24] });
      else WW.toast('This layer has no drawable extent.', 'warn');
    } catch { WW.toast('Could not fit this layer.', 'warn'); }
  }

  function exportLayer(entry) {
    if (!entry.data) { WW.toast('Turn the layer on to load its features first.', 'warn'); return; }
    downloadJson(entry.data, entry.def.key + '.geojson');
  }

  function downloadJson(data, filename) {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/geo+json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob); link.download = filename;
    document.body.appendChild(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(link.href), 2000);
  }

  function updateLegend() {
    const items = [];
    const visible = key => entries.get(key)?.visible;
    if (visible('admin-lga')) items.push({ label: 'LGA boundary', color: '#0ea5e9', line: true });
    if (visible('operational-zones')) items.push({ label: 'Service zones / DMAs', color: '#7c3aed', line: true });
    if (visible('osm-waterways')) items.push({ label: 'Rivers, streams, drainage', color: '#0284c7', line: true });
    if (visible('osm-water-bodies')) items.push({ label: 'Surface water', color: '#7dd3fc' });
    if (visible('assets')) items.push({ label: 'Water infrastructure', color: '#0284c7' });
    if (visible('sampling-stations')) items.push({ label: 'Sampling station', color: '#be123c' });
    if (visible('hydrology-stations')) items.push({ label: 'Hydrology station', color: '#0369a1' });
    if (visible('devices')) items.push({ label: 'IoT device', color: '#ca8a04' });
    if (visible('flood-layers')) items.push({ label: 'Flood layer (check type)', color: '#3b82f6' });
    if (visible('work-orders')) items.push({ label: 'Work order', color: '#ea580c' });
    el.legend.innerHTML = '<h4>Legend</h4>' + (items.length ? items.map(i =>
      '<div class="legend-row"><span class="' + (i.line ? 'legend-line' : 'legend-swatch') + '" style="background:' + i.color + '"></span><span>' + WW.escape(i.label) + '</span></div>').join('') :
      '<div class="tiny muted">No vector layer visible.</div>');
  }

  function selectFeature(layerKey, feature, layer) {
    selected = { layerKey, feature };
    selectionMarker.clearLayers();
    if (feature.geometry && feature.geometry.type === 'Point') {
      L.circleMarker([feature.geometry.coordinates[1], feature.geometry.coordinates[0]], {
        radius: 11, color: '#f59e0b', weight: 2, fillOpacity: 0
      }).addTo(selectionMarker);
    } else if (layer && layer.getBounds) {
      L.rectangle(layer.getBounds(), { color: '#f59e0b', weight: 2, fillOpacity: 0, dashArray: '4 3' }).addTo(selectionMarker);
    }
    if (layer && layer.bindPopup) layer.bindPopup(WW.popupHtml(feature)).openPopup();
    renderInspector(feature, layerKey);
  }

  function renderInspector(feature, layerKey) {
    const p = feature.properties || {};
    const rows = Object.keys(p).filter(k => !['geometry', 'id'].includes(k) && p[k] !== null && p[k] !== '').slice(0, 28)
      .map(k => '<dt>' + WW.escape(prettify(k)) + '</dt><dd>' + WW.escape(p[k]) + '</dd>').join('');
    const id = p.id || feature.id;
    let actions = '';
    if (layerKey === 'assets' && id) {
      actions += '<a class="btn btn-sm btn-primary" href="/Infrastructure/Details/' + encodeURIComponent(id) + '"><i class="fa fa-external-link"></i> Asset detail</a> ';
      if (canEdit) actions += '<button class="btn btn-sm" id="inspector-edit" type="button"><i class="fa fa-pencil"></i> Edit location</button> ';
    }
    if (layerKey === 'sampling-stations' && id) {
      actions += '<a class="btn btn-sm btn-primary" href="/WaterQuality/Station/' + encodeURIComponent(id) + '"><i class="fa fa-flask"></i> Station detail</a> ';
    }
    actions += '<button class="btn btn-sm" id="inspector-buffer" type="button"><i class="fa fa-dot-circle-o"></i> Buffer</button> ';
    actions += '<button class="btn btn-sm" id="inspector-export" type="button"><i class="fa fa-download"></i> Export</button>';
    el.inspector.innerHTML =
      '<div class="inspector-title">' + WW.escape(p.name || p.assetCode || p.code || p.deviceCode || 'Selected feature') + '</div>' +
      '<div class="inspector-sub">' + WW.escape(prettify(layerKey)) + ' · ' + (p.classification ? badge(p.classification) : '<span class="badge badge-outline">Unclassified</span>') + '</div>' +
      '<div class="flex gap-1 flex-wrap mb-2">' + actions + '</div>' +
      '<dl class="kv">' + rows + '</dl>' +
      (p.source ? '<div class="availability mt-2"><i class="fa fa-book"></i><div>Source: ' + WW.escape(p.source) + '</div></div>' : '');
    const edit = document.getElementById('inspector-edit');
    if (edit) edit.addEventListener('click', () => startAssetLocationEdit(id));
    const buffer = document.getElementById('inspector-buffer');
    if (buffer) buffer.addEventListener('click', () => openBuffer());
    const exp = document.getElementById('inspector-export');
    if (exp) exp.addEventListener('click', () => {
      downloadJson({ type: 'FeatureCollection', features: [feature] }, 'waterwatch-selected-feature.geojson');
    });
  }

  function prettify(key) { return key.replace(/([A-Z])/g, ' $1').replace(/^./, c => c.toUpperCase()); }

  function bindTools() {
    document.getElementById('tool-select').addEventListener('click', () => { cancelDraw(); setTool('tool-select'); activeTool = 'select'; });
    document.getElementById('tool-distance').addEventListener('click', () => startDraw('distance', 'tool-distance'));
    document.getElementById('tool-area').addEventListener('click', () => startDraw('area', 'tool-area'));
    document.getElementById('tool-draw-point').addEventListener('click', () => startDraw('point', 'tool-draw-point'));
    document.getElementById('tool-draw-line').addEventListener('click', () => startDraw('line', 'tool-draw-line'));
    document.getElementById('tool-draw-polygon').addEventListener('click', () => startDraw('polygon', 'tool-draw-polygon'));
    document.getElementById('tool-edit').addEventListener('click', () => {
      if (selected && selected.layerKey === 'assets') startAssetLocationEdit(selected.feature.properties?.id);
      else WW.toast('Select an asset first.', 'warn');
    });
    document.getElementById('tool-buffer').addEventListener('click', openBuffer);
    document.getElementById('tool-export').addEventListener('click', () => {
      if (selected) downloadJson({ type: 'FeatureCollection', features: [selected.feature] }, 'waterwatch-selected.geojson');
      else {
        const active = Array.from(entries.values()).filter(e => e.visible && e.data);
        if (!active.length) { WW.toast('Select a feature or enable a vector layer.', 'warn'); return; }
        const features = active.flatMap(e => e.data.features || []);
        downloadJson({ type: 'FeatureCollection', features }, 'waterwatch-active-layers.geojson');
      }
    });
    document.getElementById('tool-print').addEventListener('click', () => { map.invalidateSize(); setTimeout(() => window.print(), 250); });
    document.getElementById('tool-fullscreen').addEventListener('click', toggleFullscreen);
    document.getElementById('tool-reset').addEventListener('click', resetExtent);
    document.getElementById('refresh-layers').addEventListener('click', loadLayerCatalogue);
    document.getElementById('collapse-layers').addEventListener('click', () => {
      layout.classList.toggle('no-layers');
      setTimeout(() => map.invalidateSize(), 220);
    });
    document.getElementById('collapse-inspector').addEventListener('click', () => {
      layout.classList.toggle('no-inspector');
      setTimeout(() => map.invalidateSize(), 220);
    });
    document.getElementById('apply-filters').addEventListener('click', async () => {
      const entry = entries.get('assets');
      if (!entry) return;
      entry.loaded = false;
      if (entry.layer && map.hasLayer(entry.layer)) map.removeLayer(entry.layer);
      try { await toggleLayer(entry, true); } catch (e) { WW.toast(e.message, 'error'); }
      WW.toast('Asset filters applied.', 'warn');
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') { cancelDraw(); setTool('tool-select'); activeTool = 'select'; } });
  }

  function cancelDraw() {
    if (currentDraw) { try { currentDraw.disable(); } catch { } currentDraw = null; }
    setHint('');
  }

  function startDraw(mode, buttonId) {
    cancelDraw();
    setTool(buttonId);
    activeTool = mode;
    if (mode === 'distance') {
      currentDraw = new L.Draw.Polyline(map, { shapeOptions: { color: '#6d5bd0', weight: 3 }, showLength: true });
      setHint('Click to trace a line; double-click to finish. Distance is geodesic.');
    } else if (mode === 'area') {
      currentDraw = new L.Draw.Polygon(map, { shapeOptions: { color: '#6d5bd0', weight: 2, fillOpacity: 0.15 }, showArea: true });
      setHint('Click vertices; double-click to close. Area is geodesic.');
    } else if (mode === 'point') {
      currentDraw = new L.Draw.Marker(map);
      setHint('Click to place a point.' + (canEdit ? ' You can save it as an asset.' : ''));
    } else if (mode === 'line') {
      currentDraw = new L.Draw.Polyline(map, { shapeOptions: { color: '#0f8b8d', weight: 3 } });
      setHint('Draw a line; double-click to finish.' + (canEdit ? ' You can save it as a pipeline asset.' : ''));
    } else if (mode === 'polygon') {
      currentDraw = new L.Draw.Polygon(map, { shapeOptions: { color: '#0f8b8d', weight: 2, fillOpacity: 0.15 } });
      setHint('Draw a polygon; double-click to close.');
    }
    currentDraw.enable();
  }

  function onDrawCreated(e) {
    const layer = e.layer;
    drawnGroup.addLayer(layer); const id = 'scratch-' + (++scratchCounter);
    const type = e.layerType;
    if (activeTool === 'distance') {
      const latlngs = layer.getLatLngs(); let total = 0;
      for (let i = 1; i < latlngs.length; i++) total += latlngs[i - 1].distanceTo(latlngs[i]);
      const text = total >= 1000 ? (total / 1000).toFixed(3) + ' km' : total.toFixed(1) + ' m';
      layer.bindPopup('Measured distance: <strong>' + text + '</strong>').openPopup();
      addScratch('Distance ' + id, text + ' (geodesic; drawn scratch geometry)', layer);
      setTool('tool-select'); activeTool = 'select'; setHint('');
      return;
    }
    if (activeTool === 'area') {
      const area = (typeof L.GeometryUtil !== 'undefined' && L.GeometryUtil.geodesicArea) ? L.GeometryUtil.geodesicArea(layer.getLatLngs()[0]) : 0;
      const text = area >= 1e6 ? (area / 1e6).toFixed(3) + ' km²' : area.toFixed(1) + ' m²';
      layer.bindPopup('Measured area: <strong>' + text + '</strong>').openPopup();
      addScratch('Area ' + id, text + ' (geodesic; drawn scratch geometry)', layer);
      setTool('tool-select'); activeTool = 'select'; setHint('');
      return;
    }
    if (activeTool === 'point') {
      const latlng = layer.getLatLng();
      layer.bindPopup('Drawn point<br>' + latlng.lat.toFixed(5) + ', ' + latlng.lng.toFixed(5)).openPopup();
      if (canEdit) openAssetModal(latlng, { type: 'Point', coordinates: [latlng.lng, latlng.lat] });
      else addScratch('Point ' + id, latlng.lat.toFixed(5) + ', ' + latlng.lng.toFixed(5) + ' (not saved)', layer);
      setTool('tool-select'); activeTool = 'select'; setHint('');
      return;
    }
    if (activeTool === 'line') {
      const latlngs = layer.getLatLngs(); let total = 0;
      for (let i = 1; i < latlngs.length; i++) total += latlngs[i - 1].distanceTo(latlngs[i]);
      const geojson = layer.toGeoJSON().geometry;
      if (canEdit) openAssetModal(null, geojson, 'line');
      else addScratch('Line ' + id, total.toFixed(1) + ' m (not saved)', layer);
      setTool('tool-select'); activeTool = 'select'; setHint('');
      return;
    }
    if (activeTool === 'polygon') {
      addScratch('Polygon ' + id, 'Drawn scratch geometry; use Buffer or export.', layer);
      setTool('tool-select'); activeTool = 'select'; setHint('');
    }
  }

  function addScratch(title, text, layer) {
    const item = document.createElement('div');
    item.style.cssText = 'padding:7px 0;border-bottom:1px dashed #e7eef5';
    item.innerHTML = '<div class="flex-between"><strong class="small">' + WW.escape(title) + '</strong><button class="btn btn-sm" type="button"><i class="fa fa-eye-slash"></i></button></div><div class="tiny muted">' + WW.escape(text) + '</div>';
    item.querySelector('button').addEventListener('click', () => {
      if (layer) { if (map.hasLayer(layer)) map.removeLayer(layer); else layer.addTo(map); }
      item.remove();
    });
    el.scratch.prepend(item);
  }

  function openAssetModal(latlng, geometry, mode) {
    const form = document.getElementById('asset-form');
    form.reset();
    document.getElementById('asset-type-select').innerHTML = assetTypes.map(t => '<option>' + t + '</option>').join('');
    document.getElementById('asset-geometry').value = geometry ? JSON.stringify(geometry) : '';
    const isLine = mode === 'line' || (geometry && geometry.type !== 'Point');
    document.getElementById('asset-point-fields').style.display = isLine ? 'none' : '';
    if (isLine) document.getElementById('asset-type-select').value = 'DistributionMain';
    if (latlng) {
      document.getElementById('asset-lat').value = latlng.lat.toFixed(6);
      document.getElementById('asset-lon').value = latlng.lng.toFixed(6);
    } else {
      document.getElementById('asset-lat').value = '';
      document.getElementById('asset-lon').value = '';
    }
    document.getElementById('asset-modal').querySelector('.modal-head h3').textContent = isLine ? 'Create pipeline asset from map' : 'Create asset from map';
    WW.modal.open('asset-modal');
  }

  function bindForms() {
    document.getElementById('asset-form').addEventListener('submit', async e => {
      e.preventDefault();
      const form = e.currentTarget;
      const data = new FormData(form);
      const geometry = data.get('geometryGeoJson');
      const body = {
        assetCode: data.get('assetCode'), name: data.get('name'),
        assetType: data.get('assetType'), description: data.get('description') || null,
        status: data.get('status') || 'Unknown', condition: data.get('condition') || 'Unknown',
        adminAreaId: data.get('adminAreaId') || null, operationalAreaId: data.get('operationalAreaId') || null,
        specificationsJson: data.get('specificationsJson') || null,
        isSensitive: data.get('isSensitive') === 'on',
        longitude: null, latitude: null, geometryGeoJson: geometry || null
      };
      if (!geometry) {
        body.latitude = Number(data.get('latitude'));
        body.longitude = Number(data.get('longitude'));
      }
      try {
        const created = await WW.post('/api/assets', body);
        WW.modal.close('asset-modal');
        WW.toast('Asset ' + created.assetCode + ' created.', 'warn', 'Saved');
        const entry = entries.get('assets');
        if (entry) {
          entry.loaded = false;
          if (entry.layer && map.hasLayer(entry.layer)) map.removeLayer(entry.layer);
          await toggleLayer(entry, true);
        }
      } catch (ex) { WW.toast(ex.message, 'error', 'Create asset'); }
    });

    document.getElementById('run-buffer').addEventListener('click', runBuffer);
  }

  function openBuffer() {
    const radius = document.getElementById('buffer-radius');
    radius.value = radius.value || 500;
    document.getElementById('buffer-anchor').textContent = selected
      ? 'Selected feature: ' + (selected.feature.properties?.name || selected.feature.properties?.assetCode || 'feature')
      : 'Map centre: ' + map.getCenter().lat.toFixed(5) + ', ' + map.getCenter().lng.toFixed(5);
    WW.modal.open('buffer-modal');
  }

  async function runBuffer() {
    const radius = Number(document.getElementById('buffer-radius').value);
    const body = { radiusMeters: radius };
    if (selected && selected.feature.properties?.id) {
      body.layerKey = selected.layerKey;
      body.featureId = selected.feature.properties.id;
    } else {
      const center = map.getCenter();
      body.latitude = center.lat; body.longitude = center.lng;
    }
    try {
      const result = await WW.post('/api/analytics/buffer', body);
      analysisGroup.clearLayers();
      L.geoJSON(JSON.parse(result.geometryGeoJson), { style: { color: '#6d5bd0', weight: 2, dashArray: '6 4', fillColor: '#8b5cf6', fillOpacity: 0.12 } }).addTo(analysisGroup);
      const bounds = analysisGroup.getBounds();
      if (bounds.isValid()) map.fitBounds(bounds, { padding: [30, 30] });
      WW.modal.close('buffer-modal');
      el.inspector.innerHTML = '<div class="inspector-title">Buffer analysis</div><div class="inspector-sub">' + WW.escape(result.inputLabel) + ' · ' + result.radiusMeters + ' m</div>' +
        '<div class="callout-grid"><div class="callout"><div class="label">Buffer area</div><div class="value">' + WW.fmt(result.bufferAreaSqKm, 3) + ' km²</div></div>' +
        '<div class="callout"><div class="label">Assets inside</div><div class="value">' + result.assetsInside + '</div></div>' +
        '<div class="callout"><div class="label">Stations / devices</div><div class="value">' + result.samplingStationsInside + ' / ' + result.devicesInside + '</div></div></div>' +
        '<div class="availability mt-2"><i class="fa fa-info-circle"></i><div>' + WW.escape(result.method) + '</div></div>' +
        (result.assetNames?.length ? '<div class="mt-2 small"><strong>Assets:</strong> ' + result.assetNames.map(WW.escape).join(', ') + '</div>' : '');
      addScratch('Buffer ' + radius + ' m', result.assetNames?.length + ' named assets inside', null);
    } catch (e) { WW.toast(e.message, 'error', 'Buffer'); }
  }

  async function startAssetLocationEdit(assetId) {
    if (!canEdit) return;
    if (!assetId) { WW.toast('Asset identifier missing.', 'warn'); return; }
    try {
      const detail = await WW.get('/api/assets/' + assetId);
      if (detail.assetType === 'DistributionMain' || detail.assetType === 'TransmissionMain') {
        WW.toast('Line geometry editing is not enabled for pipelines in this release. Import a corrected line layer and re-create the asset.', 'warn');
        return;
      }
      if (!detail.latitude || !detail.longitude) { WW.toast('This asset has no point geometry to move.', 'warn'); return; }
      selectionMarker.clearLayers();
      const marker = L.marker([detail.latitude, detail.longitude], { draggable: true }).addTo(selectionMarker);
      pendingEdit = { detail, marker };
      el.inspector.innerHTML = '<div class="inspector-title">Edit asset location</div><div class="inspector-sub">' + WW.escape(detail.name) + '</div>' +
        '<div class="alert alert-warn"><i class="fa fa-info-circle"></i><div>Drag the marker to the corrected location, then save. The change is authorised server-side and audited.</div></div>' +
        '<button class="btn btn-primary" id="save-geometry" type="button"><i class="fa fa-save"></i> Save new location</button> ' +
        '<button class="btn" id="cancel-edit" type="button">Cancel</button>' +
        '<div class="small muted mt-2" id="edit-coords">' + detail.latitude.toFixed(5) + ', ' + detail.longitude.toFixed(5) + '</div>';
      marker.on('drag', () => {
        const ll = marker.getLatLng();
        document.getElementById('edit-coords').textContent = ll.lat.toFixed(5) + ', ' + ll.lng.toFixed(5);
      });
      document.getElementById('cancel-edit').addEventListener('click', () => { selectionMarker.clearLayers(); pendingEdit = null; });
      document.getElementById('save-geometry').addEventListener('click', saveAssetLocation);
    } catch (e) { WW.toast(e.message, 'error', 'Edit asset'); }
  }

  async function saveAssetLocation() {
    if (!pendingEdit) return;
    const ll = pendingEdit.marker.getLatLng();
    const d = pendingEdit.detail;
    const body = {
      assetCode: d.assetCode, name: d.name, assetType: d.assetType, description: d.description,
      status: d.status, condition: d.condition, adminAreaId: d.adminAreaId, operationalAreaId: d.operationalAreaId,
      owner: d.owner, operator: d.operator, installedOn: d.installedOn, capacityM3PerDay: d.capacityM3PerDay,
      specificationsJson: d.specificationsJson, designParametersJson: d.designParametersJson,
      isSensitive: d.isSensitive, sourceReference: d.sourceReference,
      latitude: ll.lat, longitude: ll.lng, geometryGeoJson: null
    };
    try {
      await WW.put('/api/assets/' + d.id, body);
      WW.toast('Asset location updated.', 'warn', 'Saved');
      selectionMarker.clearLayers(); pendingEdit = null;
      const entry = entries.get('assets');
      if (entry) { entry.loaded = false; if (entry.layer && map.hasLayer(entry.layer)) map.removeLayer(entry.layer); await toggleLayer(entry, true); }
    } catch (e) { WW.toast(e.message, 'error', 'Update asset'); }
  }

  function bindFilters() { }

  function bindBasemaps() {
    document.querySelectorAll('[data-basemap]').forEach(button => {
      button.classList.toggle('btn-primary', button.dataset.basemap === activeBase);
      button.addEventListener('click', () => {
        const key = button.dataset.basemap;
        if (activeBase === key) return;
        map.removeLayer(baseLayers[activeBase]);
        baseLayers[key].addTo(map); baseLayers[key].bringToBack();
        activeBase = key;
        document.querySelectorAll('[data-basemap]').forEach(b => b.classList.toggle('btn-primary', b.dataset.basemap === key));
      });
    });
  }

  function bindSearch() {
    let timer = null;
    el.search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => runSearch(el.search.value), 320); });
    el.search.addEventListener('focus', () => { if (el.search.value.trim().length >= 2) runSearch(el.search.value); });
    document.getElementById('coordinate-go').addEventListener('click', goToCoordinate);
    document.getElementById('coordinate-input').addEventListener('keydown', e => { if (e.key === 'Enter') goToCoordinate(); });
    document.addEventListener('click', e => { if (!e.target.closest('.map-search')) el.searchResults.classList.remove('open'); });
  }

  async function runSearch(term) {
    term = (term || '').trim();
    if (term.length < 2) { el.searchResults.classList.remove('open'); return; }
    el.searchResults.classList.add('open');
    el.searchResults.innerHTML = '<div class="search-empty"><i class="fa fa-spinner fa-spin"></i> Searching…</div>';
    const results = [];
    try {
      const local = await WW.get('/api/geography/search?q=' + encodeURIComponent(term));
      local.forEach(r => results.push({ label: r.label, detail: r.type + (r.subtitle ? ' · ' + r.subtitle : ''), lat: r.latitude, lon: r.longitude, layerKey: r.layerKey, url: r.url }));
    } catch { }
    try {
      const geo = await WW.get('/api/geocode?q=' + encodeURIComponent(term));
      geo.forEach(r => results.push({ label: r.label, detail: 'Nominatim · ' + (r.type || 'place'), lat: r.latitude, lon: r.longitude }));
    } catch { }
    if (!results.length) { el.searchResults.innerHTML = '<div class="search-empty">No matching place or feature. Try coordinates.</div>'; return; }
    el.searchResults.innerHTML = results.slice(0, 12).map((r, i) =>
      '<button type="button" data-index="' + i + '"><strong>' + WW.escape(r.label) + '</strong><div class="tiny muted">' + WW.escape(r.detail) + '</div></button>').join('');
    el.searchResults.querySelectorAll('button').forEach(button => button.addEventListener('click', () => {
      const r = results[Number(button.dataset.index)];
      if (r.lat && r.lon) {
        map.setView([r.lat, r.lon], 16);
        selectionMarker.clearLayers();
        L.marker([r.lat, r.lon]).addTo(selectionMarker).bindPopup(WW.escape(r.label)).openPopup();
      }
      el.searchResults.classList.remove('open');
    }));
  }

  function goToCoordinate() {
    const value = document.getElementById('coordinate-input').value.trim();
    const parts = value.split(/[,\s]+/).map(Number).filter(n => !Number.isNaN(n));
    if (parts.length < 2) { WW.toast('Enter latitude and longitude, e.g. 4.8156, 7.0498.', 'warn'); return; }
    let lat = parts[0], lon = parts[1];
    if (Math.abs(lat) > 90 && Math.abs(lon) <= 90) { const t = lat; lat = lon; lon = t; }
    if (Math.abs(lat) > 90 || Math.abs(lon) > 180) { WW.toast('Coordinates are outside valid latitude/longitude bounds.', 'warn'); return; }
    map.setView([lat, lon], 17);
    selectionMarker.clearLayers();
    L.marker([lat, lon]).addTo(selectionMarker).bindPopup(lat + ', ' + lon).openPopup();
    el.searchResults.classList.remove('open');
  }

  function resetExtent() {
    if (context?.extent) map.fitBounds([[context.extent.minLat, context.extent.minLon], [context.extent.maxLat, context.extent.maxLon]], { padding: [24, 24] });
    else map.setView([4.8156, 7.0498], 12);
  }

  function toggleFullscreen() {
    const target = document.querySelector('.gis-map-pane');
    if (!document.fullscreenElement) {
      target.requestFullscreen?.().then(() => setTimeout(() => map.invalidateSize(), 250));
    } else {
      document.exitFullscreen?.().then(() => setTimeout(() => map.invalidateSize(), 250));
    }
  }

  function bindBookmarks() {
    const defaults = [
      { name: 'Port Harcourt centre', lat: 4.8156, lon: 7.0498, zoom: 14 },
      { name: 'Old GRA / Diobu', lat: 4.7999, lon: 7.0073, zoom: 15 },
      { name: 'Rumuomasi', lat: 4.8330, lon: 7.0292, zoom: 15 },
      { name: 'Woji', lat: 4.8163, lon: 6.9894, zoom: 15 },
      { name: 'Elelenwo', lat: 4.8426, lon: 7.0752, zoom: 14 }
    ];
    const saved = JSON.parse(localStorage.getItem('ww.bookmarks') || '[]');
    const list = document.getElementById('bookmark-list');
    const render = () => {
      const all = defaults.concat(saved);
      list.innerHTML = all.map((b, i) =>
        '<div class="flex-between" style="padding:4px 0"><button class="btn btn-sm" style="justify-content:flex-start;flex:1" data-bm="' + i + '"><i class="fa fa-bookmark-o"></i> ' + WW.escape(b.name) + '</button>' +
        (i >= defaults.length ? '<button class="btn btn-sm btn-danger" data-del="' + (i - defaults.length) + '"><i class="fa fa-times"></i></button>' : '') + '</div>').join('');
      list.querySelectorAll('[data-bm]').forEach(btn => btn.addEventListener('click', () => {
        const b = all[Number(btn.dataset.bm)];
        map.setView([b.lat, b.lon], b.zoom || 14);
      }));
      list.querySelectorAll('[data-del]').forEach(btn => btn.addEventListener('click', () => {
        const s = JSON.parse(localStorage.getItem('ww.bookmarks') || '[]');
        s.splice(Number(btn.dataset.del), 1);
        localStorage.setItem('ww.bookmarks', JSON.stringify(s));
        render();
      }));
    };
    document.getElementById('save-bookmark').addEventListener('click', () => {
      const name = window.prompt('Bookmark name', 'My map view');
      if (!name) return;
      const center = map.getCenter();
      const s = JSON.parse(localStorage.getItem('ww.bookmarks') || '[]');
      s.push({ name, lat: center.lat, lon: center.lng, zoom: map.getZoom() });
      localStorage.setItem('ww.bookmarks', JSON.stringify(s));
      render();
    });
    render();
  }

  init();
})();
