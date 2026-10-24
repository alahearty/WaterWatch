(function () {
  'use strict';
  const state = { scenes: [], left: null, right: null };

  document.addEventListener('DOMContentLoaded', init);

  function init() {
    const to = new Date();
    const from = new Date(); from.setMonth(from.getMonth() - 3);
    document.getElementById('rs-from').value = from.toISOString().slice(0, 10);
    document.getElementById('rs-to').value = to.toISOString().slice(0, 10);
    document.getElementById('rs-search').addEventListener('click', search);
    document.getElementById('rs-range').addEventListener('input', updateClip);
    document.getElementById('rs-left').addEventListener('change', e => { state.left = state.scenes.find(s => s.sceneId === e.target.value); renderSlider(); });
    document.getElementById('rs-right').addEventListener('change', e => { state.right = state.scenes.find(s => s.sceneId === e.target.value); renderSlider(); });
    loadChangeLayers();
  }

  async function search() {
    const collection = document.getElementById('rs-collection').value;
    const from = document.getElementById('rs-from').value;
    const to = document.getElementById('rs-to').value;
    const maxCloudPercent = Number(document.getElementById('rs-cloud').value);
    document.getElementById('rs-status').textContent = 'Searching…';
    document.getElementById('rs-status').className = 'badge badge-amber';
    document.getElementById('rs-scenes').innerHTML = '<div class="loading-block"><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div></div>';
    try {
      const result = await WW.post('/api/remote-sensing/search', { collection, from, to, maxCloudPercent, limit: 12 });
      state.scenes = result.scenes || [];
      document.getElementById('rs-status').textContent = result.status === 'available' ? 'Live catalogue' : 'Catalogue unavailable';
      document.getElementById('rs-status').className = 'badge ' + (result.status === 'available' ? 'badge-green' : 'badge-red');
      document.getElementById('rs-count').textContent = state.scenes.length + ' scenes · ' + result.source;
      document.getElementById('rs-availability').innerHTML = result.status === 'available'
        ? '<div class="alert alert-info"><i class="fa fa-info-circle"></i><div>Metadata retrieved from ' + WW.escape(result.source) + ' at ' + WW.fmtDate(result.retrievedAt, true) + '. Sentinel and Landsat data are open; previews are not analysis-ready products.</div></div>'
        : '<div class="alert alert-warn"><i class="fa fa-satellite"></i><div>' + WW.escape(result.message || 'STAC unavailable.') + '</div></div>';
      renderScenes();
    } catch (e) {
      document.getElementById('rs-status').textContent = 'Search failed';
      document.getElementById('rs-status').className = 'badge badge-red';
      document.getElementById('rs-scenes').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>';
    }
  }

  function renderScenes() {
    if (!state.scenes.length) {
      document.getElementById('rs-scenes').innerHTML = WW.empty('No scenes', 'No scenes matched the date and cloud filters.', 'fa-satellite');
      return;
    }
    document.getElementById('rs-scenes').innerHTML = '<div class="scene-grid">' + state.scenes.map(s =>
      '<div class="scene-card" data-scene="' + WW.escape(s.sceneId) + '"><div class="scene-thumb" style="background-image:url(\'' + encodeURI(s.thumbnailUrl || '') + '\')"><span class="badge badge-grey cloud">☁ ' + (s.cloudCover ?? '—') + '%</span></div>' +
      '<div class="scene-meta"><strong>' + WW.escape(s.collection) + '</strong><div>' + WW.fmtDate(s.acquiredAt) + ' · ' + (s.resolutionM || '—') + ' m</div>' +
      '<div class="tiny muted">' + WW.escape(s.sceneId.slice(0, 28)) + '</div>' +
      '<div class="flex gap-1 mt-1"><button class="btn btn-sm" data-left="' + WW.escape(s.sceneId) + '">Left</button><button class="btn btn-sm" data-right="' + WW.escape(s.sceneId) + '">Right</button></div></div></div>').join('') + '</div>';
    document.querySelectorAll('[data-left]').forEach(btn => btn.addEventListener('click', e => { e.stopPropagation(); state.left = state.scenes.find(s => s.sceneId === btn.dataset.left); syncSelects(); }));
    document.querySelectorAll('[data-right]').forEach(btn => btn.addEventListener('click', e => { e.stopPropagation(); state.right = state.scenes.find(s => s.sceneId === btn.dataset.right); syncSelects(); }));
    state.left = state.scenes[0]; state.right = state.scenes[1] || state.scenes[0];
    syncSelects();
  }

  function syncSelects() {
    const options = state.scenes.map(s => '<option value="' + WW.escape(s.sceneId) + '">' + WW.escape(s.collection + ' · ' + WW.fmtDate(s.acquiredAt) + ' · ☁' + (s.cloudCover ?? '—') + '%') + '</option>').join('');
    const left = document.getElementById('rs-left'); const right = document.getElementById('rs-right');
    left.innerHTML = options; right.innerHTML = options;
    if (state.left) left.value = state.left.sceneId;
    if (state.right) right.value = state.right.sceneId;
    renderSlider();
  }

  function renderSlider() {
    const before = document.getElementById('rs-before'); const after = document.getElementById('rs-after');
    if (state.left?.thumbnailUrl) before.src = state.left.thumbnailUrl; else before.removeAttribute('src');
    if (state.right?.thumbnailUrl) after.src = state.right.thumbnailUrl; else after.removeAttribute('src');
    document.querySelector('.split-label.before').textContent = 'Before · ' + (state.left ? WW.fmtDate(state.left.acquiredAt) : '—');
    document.querySelector('.split-label.after').textContent = 'After · ' + (state.right ? WW.fmtDate(state.right.acquiredAt) : '—');
    updateClip();
  }

  function updateClip() {
    const value = Number(document.getElementById('rs-range').value);
    document.getElementById('rs-after-wrap').style.width = value + '%';
  }

  async function loadChangeLayers() {
    try {
      const layers = await WW.get('/api/remote-sensing/change-layers');
      document.getElementById('rs-change-layers').innerHTML = layers.length
        ? layers.map(l => '<div class="mb-2"><div class="flex-between"><strong>' + WW.escape(l.name) + '</strong>' + WW.classification(l.dataClassification) + '</div><div class="small muted">' + WW.escape(l.description || '') + ' · ' + WW.escape(l.sourceName) + '</div><div class="table-wrap mt-1"><table class="ww-table"><thead><tr><th>Class</th><th class="num">Area km²</th><th class="num">Features</th><th class="num">Share</th></tr></thead><tbody>' +
          l.classes.map(c => '<tr><td>' + WW.escape(c.className) + '</td><td class="num">' + WW.fmt(c.areaSqKm, 2) + '</td><td class="num">' + c.featureCount + '</td><td class="num">' + (c.percentOfLayer != null ? WW.fmt(c.percentOfLayer, 1) + '%' : '—') + '</td></tr>').join('') + '</tbody></table></div></div>').join('')
        : WW.empty('No change layers imported', 'Register a derived water extent, land-cover or built-up change layer through the import pipeline with its processing parameters and source. The application does not fabricate change statistics from thumbnails.', 'fa-layer-group');
    } catch (e) { document.getElementById('rs-change-layers').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>'; }
  }
})();
