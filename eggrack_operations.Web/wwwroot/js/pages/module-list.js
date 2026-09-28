(() => {
  const root = document.querySelector('[data-module-list]');
  if (!root) return;
  const search = root.querySelector('[data-search]');
  const status = root.querySelector('[data-status]');
  const rows = [...root.querySelectorAll('[data-row]')];
  const empty = root.querySelector('[data-empty]');
  const count = root.querySelector('[data-count]');
  const drawer = bootstrap.Offcanvas.getOrCreateInstance(root.querySelector('#moduleDetail'));
  function filter() {
    const keyword = search.value.trim().toLowerCase();
    let visible = 0;
    rows.forEach(row => {
      const show = (!keyword || row.textContent.toLowerCase().includes(keyword)) && (!status.value || row.dataset.status === status.value);
      row.hidden = !show;
      if (show) visible++;
    });
    empty.hidden = visible !== 0;
    count.textContent = '共 ' + visible + ' 条';
  }
  rows.forEach(row => {
    const open = () => {
      const cells = [...row.querySelectorAll('td[data-label]')];
      root.querySelector('[data-detail-title]').textContent = cells[0]?.textContent.trim() || row.dataset.id;
      root.querySelector('[data-detail-body]').innerHTML = '<div class="module-detail-card"><small class="text-secondary">记录编号</small><strong>' + row.dataset.id + '</strong><span>' + row.dataset.status + '</span></div>' + cells.map(cell => '<div class="module-detail-row"><span>' + cell.dataset.label + '</span><strong>' + cell.textContent.trim() + '</strong></div>').join('');
      drawer.show();
    };
    row.addEventListener('click', open);
    row.querySelector('[data-detail]').addEventListener('click', event => { event.stopPropagation(); open(); });
  });
  search.addEventListener('input', filter);
  status.addEventListener('change', filter);
  root.querySelector('[data-reset]').addEventListener('click', () => { search.value = ''; status.value = ''; filter(); });
  document.querySelector('.ui-page-actions .btn-primary')?.addEventListener('click', event => { event.preventDefault(); alert('页面框架已就绪，数据写入将在对应模块后端接入后启用。'); });
  filter();
})();