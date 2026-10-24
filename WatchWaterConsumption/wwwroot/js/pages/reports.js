(function () {
  'use strict';
  let types = [];

  document.addEventListener('DOMContentLoaded', init);

  async function init() {
    try {
      types = await WW.get('/api/reports/types');
      document.getElementById('report-type').innerHTML = types.map(t => '<option value="' + t.key + '">' + WW.escape(t.name) + '</option>').join('');
      document.getElementById('report-types').innerHTML = types.map(t =>
        '<div class="callout mb-1"><div class="flex-between"><strong class="small">' + WW.escape(t.name) + '</strong><span class="badge badge-outline">' + t.supportedFormats.join(' / ') + '</span></div><div class="tiny muted mt-1">' + WW.escape(t.description) + '</div></div>').join('');
      document.getElementById('generate-report').addEventListener('click', generate);
      await loadJobs();
    } catch (e) {
      document.getElementById('report-types').innerHTML = '<div class="alert alert-danger">' + WW.escape(e.message) + '</div>';
    }
  }

  async function generate() {
    const type = document.getElementById('report-type').value;
    const format = document.getElementById('report-format').value;
    const params = new URLSearchParams({ format });
    const from = document.getElementById('report-from').value;
    const to = document.getElementById('report-to').value;
    const search = document.getElementById('report-search').value.trim();
    const dimension = document.getElementById('report-dimension').value;
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    if (search) params.set('search', search);
    if (dimension) params.set('dimension', dimension);
    const button = document.getElementById('generate-report');
    button.disabled = true;
    document.getElementById('report-result').innerHTML = '<div class="alert alert-info"><i class="fa fa-spinner fa-spin"></i><div>Generating report…</div></div>';
    try {
      const response = await fetch('/api/reports/' + encodeURIComponent(type) + '?' + params.toString(), { credentials: 'same-origin' });
      if (!response.ok) {
        let message = 'Report generation failed (' + response.status + ').';
        try { const payload = await response.json(); message = payload.message || payload.error || message; } catch { }
        throw new Error(message);
      }
      const blob = await response.blob();
      const disposition = response.headers.get('Content-Disposition') || '';
      const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)/i.exec(disposition);
      const filename = match ? decodeURIComponent(match[1]) : 'waterwatch-report.' + format.toLowerCase();
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = filename;
      document.body.appendChild(link); link.click(); link.remove();
      setTimeout(() => URL.revokeObjectURL(link.href), 2000);
      document.getElementById('report-result').innerHTML = '<div class="alert alert-success"><i class="fa fa-check"></i><div>Report downloaded: ' + WW.escape(filename) + '.</div></div>';
      await loadJobs();
    } catch (e) {
      document.getElementById('report-result').innerHTML = '<div class="alert alert-danger"><i class="fa fa-exclamation-circle"></i><div>' + WW.escape(e.message) + '</div></div>';
    } finally { button.disabled = false; }
  }

  async function loadJobs() {
    try {
      const jobs = await WW.get('/api/reports/jobs');
      document.getElementById('report-jobs').innerHTML = jobs.length
        ? '<div class="table-wrap"><table class="ww-table"><thead><tr><th>Report</th><th>Format</th><th>Status</th><th class="num">Rows</th><th>Requested</th><th>Completed</th></tr></thead><tbody>' +
          jobs.map(j => '<tr><td>' + WW.escape(j.name) + '<div class="tiny muted">' + WW.escape(j.reportType) + '</div></td><td>' + WW.escape(j.format) + '</td><td>' + WW.status(j.status) + '</td><td class="num">' + j.recordCount + '</td><td>' + WW.fmtDate(j.createdAt, true) + '</td><td>' + WW.fmtDate(j.completedAt, true) + '</td></tr>').join('') + '</tbody></table></div>'
        : WW.empty('No reports', 'Generated reports will be listed here with their audit metadata.', 'fa-file-text-o');
    } catch (e) { document.getElementById('report-jobs').innerHTML = '<div class="alert alert-danger m-3">' + WW.escape(e.message) + '</div>'; }
  }
})();
