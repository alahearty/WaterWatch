(function () {
  'use strict';

  const WW = window.WW = window.WW || {};
  const csrf = () => {
    const input = document.querySelector('#csrf-form input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  };

  WW.escape = function (value) {
    if (value === null || value === undefined) return '';
    return String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  };
  WW.escapeAttr = WW.escape;
  WW.fmt = function (value, digits) {
    if (value === null || value === undefined || value === '') return '—';
    const n = Number(value);
    if (Number.isNaN(n)) return WW.escape(value);
    return n.toLocaleString(undefined, { maximumFractionDigits: digits === undefined ? 2 : digits });
  };
  WW.fmtInt = value => value === null || value === undefined ? '—' : Number(value).toLocaleString(undefined, { maximumFractionDigits: 0 });
  WW.fmtDate = function (value, withTime) {
    if (!value) return '—';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return WW.escape(value);
    return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: '2-digit' }) +
      (withTime ? ' ' + d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }) : '');
  };
  WW.fmtRelative = function (value) {
    if (!value) return '—';
    const d = new Date(value);
    const diff = (Date.now() - d.getTime()) / 1000;
    if (diff < 60) return 'just now';
    if (diff < 3600) return Math.round(diff / 60) + ' min ago';
    if (diff < 86400) return Math.round(diff / 3600) + ' h ago';
    return Math.round(diff / 86400) + ' d ago';
  };
  WW.today = () => new Date().toISOString().slice(0, 10);
  WW.monthStart = () => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10); };
  WW.addMonths = (date, months) => { const d = new Date(date); d.setMonth(d.getMonth() + months); return d.toISOString().slice(0, 10); };

  WW.toast = function (message, type, title) {
    let stack = document.querySelector('.toast-stack');
    if (!stack) { stack = document.createElement('div'); stack.className = 'toast-stack'; document.body.appendChild(stack); }
    const el = document.createElement('div');
    el.className = 'toast ' + (type || '');
    el.innerHTML = '<strong>' + WW.escape(title || (type === 'error' ? 'Error' : type === 'warn' ? 'Attention' : 'WaterWatch')) + '</strong>' +
      '<div>' + WW.escape(message) + '</div>';
    stack.appendChild(el);
    setTimeout(() => el.remove(), 6500);
  };

  function errorMessage(payload, fallback) {
    if (!payload) return fallback;
    if (typeof payload === 'string') return payload;
    if (payload.error && payload.message) return payload.error + ': ' + payload.message;
    if (payload.message) return payload.message;
    if (payload.title) return payload.title;
    if (payload.errors) return Object.values(payload.errors).flat().join(' ');
    return fallback;
  }

  WW.api = async function (url, options) {
    const opts = Object.assign({ credentials: 'same-origin' }, options || {});
    opts.headers = Object.assign({ 'Accept': 'application/json' }, opts.headers || {});
    const method = (opts.method || 'GET').toUpperCase();
    if (method !== 'GET' && method !== 'HEAD' && !(opts.body instanceof FormData)) {
      opts.headers['Content-Type'] = opts.headers['Content-Type'] || 'application/json';
    }
    if (method !== 'GET' && method !== 'HEAD') {
      opts.headers['X-CSRF-TOKEN'] = csrf();
    }
    const response = await fetch(url, opts);
    const text = await response.text();
    let payload = null;
    if (text) {
      try { payload = JSON.parse(text); } catch { payload = text; }
    }
    if (!response.ok) {
      if (response.status === 401) {
        WW.toast('Your session has expired. Please sign in again.', 'warn');
      }
      const error = new Error(errorMessage(payload, 'Request failed (' + response.status + ').'));
      error.status = response.status;
      error.payload = payload;
      throw error;
    }
    return payload;
  };
  WW.get = url => WW.api(url);
  WW.post = (url, body) => WW.api(url, { method: 'POST', body: body instanceof FormData ? body : JSON.stringify(body) });
  WW.put = (url, body) => WW.api(url, { method: 'PUT', body: JSON.stringify(body) });
  WW.del = url => WW.api(url, { method: 'DELETE' });

  WW.classification = function (value) {
    const labels = { SyntheticDemonstration: 'Synthetic demo', Estimated: 'Estimated', Modelled: 'Modelled', Imported: 'Imported', Observed: 'Observed' };
    const label = labels[value] || value || 'Unknown';
    const cls = value === 'SyntheticDemonstration' ? 'badge-synthetic' : value === 'Estimated' ? 'badge-amber' : value === 'Modelled' ? 'badge-purple' : value === 'Imported' ? 'badge-blue' : 'badge-teal';
    return '<span class="badge ' + cls + '">' + WW.escape(label) + '</span>';
  };
  WW.status = function (value) {
    if (!value) return '<span class="badge badge-grey">Unknown</span>';
    const v = String(value);
    let cls = 'badge-grey';
    if (['Operational', 'Active', 'Compliant', 'Closed', 'Verified', 'Resolved', 'Low', 'Live', 'Available'].includes(v)) cls = 'badge-green';
    else if (['UnderMaintenance', 'Maintenance', 'Assigned', 'Triaged', 'Reported', 'InProgress', 'Medium', 'Recent', 'Partial'].includes(v)) cls = 'badge-amber';
    else if (['OutOfService', 'Decommissioned', 'Critical', 'High', 'NonCompliant', 'Overdue', 'Offline', 'Open', 'Cancelled'].includes(v)) cls = 'badge-red';
    else if (['Fair', 'Good', 'Acknowledged', 'Estimated'].includes(v)) cls = 'badge-blue';
    else if (['Poor', 'InsufficientEvidence', 'NotAssessed', 'Unknown'].includes(v)) cls = 'badge-grey';
    const label = v.replace(/([A-Z])/g, ' $1').trim();
    return '<span class="badge ' + cls + '">' + WW.escape(label) + '</span>';
  };
  WW.stateBadge = function (state) {
    if (state === 'Compliant') return '<span class="badge badge-green">Compliant</span>';
    if (state === 'NonCompliant') return '<span class="badge badge-red">Non-compliant</span>';
    if (state === 'InsufficientEvidence') return '<span class="badge badge-amber">Insufficient evidence</span>';
    return '<span class="badge badge-grey">Not assessed</span>';
  };

  WW.loading = function (target, rows) {
    const el = typeof target === 'string' ? document.querySelector(target) : target;
    if (!el) return;
    el.innerHTML = '<div class="loading-block">' + Array.from({ length: rows || 4 }).map(() => '<div class="skeleton" style="height:14px"></div>').join('') + '</div>';
  };
  WW.empty = function (title, message, icon) {
    return '<div class="empty-state"><div class="icon"><i class="fa ' + (icon || 'fa-inbox') + '"></i></div><h3>' +
      WW.escape(title) + '</h3><p>' + WW.escape(message) + '</p></div>';
  };
  WW.availability = function (message, type) {
    return '<div class="alert alert-' + (type || 'neutral') + '"><i class="fa fa-info-circle"></i><div>' + WW.escape(message) + '</div></div>';
  };

  WW.chart = function (canvasId, config) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === 'undefined') return null;
    const existing = Chart.getChart(canvas);
    if (existing) existing.destroy();
    Chart.defaults.font.family = 'Source Sans Pro, Segoe UI, sans-serif';
    Chart.defaults.font.size = 11;
    Chart.defaults.color = '#5f7183';
    Chart.defaults.borderColor = '#e8eef5';
    return new Chart(canvas.getContext('2d'), config);
  };
  WW.palette = ['#0f8b8d', '#1d6fb8', '#6d5bd0', '#b7791f', '#1f9d67', '#c0392b', '#4b8ca8', '#8c6d31', '#5a7d9a', '#9b59b6'];
  WW.lineDataset = (label, data, color, fill) => ({
    label: label, data: data, borderColor: color, backgroundColor: fill ? color + '22' : 'transparent',
    borderWidth: 2, pointRadius: 2, pointHoverRadius: 4, tension: 0.28, fill: !!fill
  });
  WW.axis = (title) => ({ title: { display: !!title, text: title, color: '#5f7183', font: { size: 10, weight: '600' } }, grid: { color: '#edf2f7' }, ticks: { maxRotation: 0, autoSkip: true } });

  WW.modal = {
    open(id) { const el = document.getElementById(id); if (el) el.classList.add('open'); },
    close(id) { const el = document.getElementById(id); if (el) el.classList.remove('open'); },
    init() {
      document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
        backdrop.addEventListener('click', e => { if (e.target === backdrop) backdrop.classList.remove('open'); });
        backdrop.querySelectorAll('[data-modal-close]').forEach(btn => btn.addEventListener('click', () => backdrop.classList.remove('open')));
      });
    }
  };
  WW.tabs = function (root) {
    const container = typeof root === 'string' ? document.querySelector(root) : root;
    if (!container) return;
    container.querySelectorAll('.tabs button').forEach(button => {
      button.addEventListener('click', () => {
        const panelId = button.getAttribute('data-tab');
        container.querySelectorAll('.tabs button').forEach(b => b.classList.remove('active'));
        button.classList.add('active');
        container.querySelectorAll('.tab-panel').forEach(p => p.classList.toggle('active', p.id === panelId));
      });
    });
  };

  WW.mapTarget = {
    set(lat, lon, zoom, label) {
      sessionStorage.setItem('ww.mapTarget', JSON.stringify({ lat: lat, lon: lon, zoom: zoom || 15, label: label || '', at: Date.now() }));
    },
    take() {
      const value = sessionStorage.getItem('ww.mapTarget');
      if (!value) return null;
      sessionStorage.removeItem('ww.mapTarget');
      try { return JSON.parse(value); } catch { return null; }
    }
  };

  WW.searchNavigate = function (result) {
    if (!result) return;
    if (result.latitude && result.longitude) {
      sessionStorage.setItem('ww.mapTarget', JSON.stringify({ lat: result.latitude, lon: result.longitude, zoom: 16, label: result.label, at: Date.now() }));
    }
    if (result.url) { window.location.href = result.url; return; }
    window.location.href = '/Gis';
  };

  WW.initShell = function () {
    const shell = document.querySelector('.app-shell');
    const toggle = document.querySelector('.topbar-toggle');
    if (toggle && shell) toggle.addEventListener('click', () => shell.classList.toggle('sidebar-open'));
    document.addEventListener('click', e => {
      if (shell && shell.classList.contains('sidebar-open') && !e.target.closest('.sidebar') && !e.target.closest('.topbar-toggle')) {
        shell.classList.remove('sidebar-open');
      }
    });

    const search = document.getElementById('global-search-input');
    const results = document.getElementById('global-search-results');
    let timer = null;
    if (search && results) {
      const run = async () => {
        const q = search.value.trim();
        if (q.length < 2) { results.classList.remove('open'); results.innerHTML = ''; return; }
        results.classList.add('open');
        results.innerHTML = '<div class="search-empty"><i class="fa fa-spinner fa-spin"></i> Searching…</div>';
        try {
          const data = await WW.get('/api/geography/search?q=' + encodeURIComponent(q));
          if (!data.length) { results.innerHTML = '<div class="search-empty">No matching records. Try a coordinate search in GIS Explorer.</div>'; return; }
          results.innerHTML = data.map(r =>
            '<a class="search-result" href="#" data-result="' + WW.escape(encodeURIComponent(JSON.stringify(r))) + '">' +
            '<span class="sr-icon"><i class="fa ' + (r.type === 'Asset' ? 'fa-wrench' : r.type === 'Work order' ? 'fa-clipboard' : 'fa-map-marker') + '"></i></span>' +
            '<span><strong>' + WW.escape(r.label) + '</strong><span>' + WW.escape((r.type || '') + (r.subtitle ? ' · ' + r.subtitle : '')) + '</span></span></a>').join('');
          results.querySelectorAll('.search-result').forEach(link => link.addEventListener('click', ev => {
            ev.preventDefault();
            try { WW.searchNavigate(JSON.parse(decodeURIComponent(link.getAttribute('data-result')))); } catch { window.location.href = '/Gis'; }
          }));
        } catch (e) {
          results.innerHTML = '<div class="search-empty">Search failed: ' + WW.escape(e.message) + '</div>';
        }
      };
      search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(run, 260); });
      search.addEventListener('keydown', e => {
        if (e.key === 'Enter') {
          const first = results.querySelector('.search-result');
          if (first) first.click();
          else {
            WW.mapTarget.set(null, null, 15, search.value);
            sessionStorage.setItem('ww.mapSearchTerm', search.value);
            window.location.href = '/Gis';
          }
        }
      });
      document.addEventListener('click', e => { if (!e.target.closest('.global-search')) results.classList.remove('open'); });
    }

    const notifBtn = document.getElementById('notification-button');
    const notifPanel = document.getElementById('notification-panel');
    if (notifBtn && notifPanel) {
      notifBtn.addEventListener('click', async e => {
        e.stopPropagation();
        notifPanel.classList.toggle('open');
        if (!notifPanel.classList.contains('open')) return;
        notifPanel.innerHTML = '<div class="search-empty">Loading notifications…</div>';
        try {
          const data = await WW.get('/api/notifications');
          if (!data.length) { notifPanel.innerHTML = '<div class="search-empty">No active notifications.</div>'; notifBtn.querySelector('.dot')?.remove(); return; }
          notifPanel.innerHTML = '<h4>Notifications</h4>' + data.map(n =>
            '<a class="notification" href="' + WW.escape(n.url || '#') + '"><span class="n-icon"><i class="fa fa-bell"></i></span>' +
            '<span><strong>' + WW.escape(n.title) + '</strong><span>' + WW.escape(n.message || '') + ' · ' + WW.escape(WW.fmtRelative(n.occurredAt)) + '</span></span></a>').join('');
          notifBtn.querySelector('.dot')?.remove();
        } catch {
          notifPanel.innerHTML = '<div class="search-empty">Sign in to view notifications.</div>';
        }
      });
      document.addEventListener('click', e => { if (!e.target.closest('#notification-panel') && !e.target.closest('#notification-button')) notifPanel.classList.remove('open'); });
    }

    const userBtn = document.getElementById('user-menu-button');
    const userPanel = document.getElementById('user-menu-panel');
    if (userBtn && userPanel) {
      userBtn.addEventListener('click', e => { e.stopPropagation(); userPanel.classList.toggle('open'); });
      document.addEventListener('click', e => { if (!e.target.closest('#user-menu-panel') && !e.target.closest('#user-menu-button')) userPanel.classList.remove('open'); });
    }
    WW.modal.init();
  };

  WW.loadGeoJsonLayer = async function (map, key, style, options) {
    const data = await WW.get('/api/layers/' + encodeURIComponent(key) + '/geojson?limit=' + ((options && options.limit) || 5000));
    return L.geoJSON(data, Object.assign({
      style: style || { color: '#0f8b8d', weight: 2, fillOpacity: 0.15 },
      pointToLayer: (feature, latlng) => L.circleMarker(latlng, { radius: 5, weight: 2, fillOpacity: 0.8, color: '#0f8b8d', fillColor: '#0f8b8d' })
    }, options && options.leafletOptions || {}));
  };

  WW.popupHtml = function (feature) {
    const p = feature.properties || {};
    const priority = ['name', 'assetCode', 'assetType', 'status', 'condition', 'layerType', 'code', 'waterSourceType', 'deviceType', 'stationType', 'consumptionM3', 'classification', 'source'];
    const rows = priority.filter(k => p[k] !== undefined && p[k] !== null && p[k] !== '').map(k =>
      '<div class="popup-row"><span>' + WW.escape(prettify(k)) + '</span><span>' + WW.escape(p[k]) + '</span></div>').join('');
    return '<div class="popup-title">' + WW.escape(p.name || p.assetCode || p.code || 'Feature') + '</div>' + rows;
  };
  function prettify(key) { return key.replace(/([A-Z])/g, ' $1').replace(/^./, c => c.toUpperCase()); }

  document.addEventListener('DOMContentLoaded', () => {
    WW.initShell();
  });
})();
