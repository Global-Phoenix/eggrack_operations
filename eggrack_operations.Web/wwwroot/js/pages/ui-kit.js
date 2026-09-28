(() => {
  const root = document.querySelector('[data-ui-demo]');
  if (!root) return;
  let records = [
    { id: 1001, code: 'DEMO-001', name: '华东采购计划', category: '采购', owner: '陈嘉', status: '启用', updated: '2026-09-28 16:30' },
    { id: 1002, code: 'DEMO-002', name: '仓库周期盘点', category: '仓储', owner: '刘静', status: '启用', updated: '2026-09-28 15:12' },
    { id: 1003, code: 'DEMO-003', name: '角色权限复核', category: '系统', owner: '王悦', status: '停用', updated: '2026-09-27 11:45' },
    { id: 1004, code: 'DEMO-004', name: '供应商资料更新', category: '采购', owner: '周航', status: '启用', updated: '2026-09-26 09:20' },
    { id: 1005, code: 'DEMO-005', name: '文件清理检查', category: '系统', owner: '许岚', status: '启用', updated: '2026-09-25 18:05' },
    { id: 1006, code: 'DEMO-006', name: '入库差异复核', category: '仓储', owner: '林晓宇', status: '启用', updated: '2026-09-24 14:10' }
  ];
  const selected = new Set();
  const pageSize = 4;
  let page = 1;
  let activeId = null;
  const find = selector => root.querySelector(selector);
  const modal = bootstrap.Modal.getOrCreateInstance(find('#recordModal'));
  const detail = bootstrap.Offcanvas.getOrCreateInstance(find('#recordDetail'));
  const toast = bootstrap.Toast.getOrCreateInstance(find('[data-toast]'), { delay: 1800 });
  const escapeHtml = value => String(value).replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
  const filtered = () => {
    const keyword = find('[data-search]').value.trim().toLowerCase();
    const status = find('[data-status]').value;
    return records.filter(item => (!status || item.status === status) && (!keyword || (item.code + item.name + item.owner).toLowerCase().includes(keyword)));
  };
  function notify(message) { find('[data-toast-text]').textContent = message; toast.show(); }
  function updateSelection() {
    find('[data-selection]').textContent = selected.size ? '已选择 ' + selected.size + ' 条' : '未选择记录';
    find('[data-batch]').disabled = selected.size === 0;
    const visible = filtered().slice((page - 1) * pageSize, page * pageSize);
    find('[data-check-all]').checked = visible.length > 0 && visible.every(item => selected.has(item.id));
  }
  function render() {
    const items = filtered();
    const pages = Math.max(1, Math.ceil(items.length / pageSize));
    page = Math.min(page, pages);
    const visible = items.slice((page - 1) * pageSize, page * pageSize);
    const body = find('[data-rows]');
    body.innerHTML = '';
    visible.forEach(item => {
      const row = document.createElement('tr');
      row.dataset.id = item.id;
      row.innerHTML = '<td class="ui-check"><input class="form-check-input" type="checkbox" aria-label="选择记录" ' + (selected.has(item.id) ? 'checked' : '') + '></td><td><span class="ui-code">' + item.code + '</span></td><td><strong>' + escapeHtml(item.name) + '</strong></td><td>' + item.category + '</td><td><div class="ui-person"><span>' + escapeHtml(item.owner.slice(0,1)) + '</span>' + escapeHtml(item.owner) + '</div></td><td><span class="ui-badge ' + (item.status === '启用' ? 'enabled' : 'disabled') + '">' + item.status + '</span></td><td>' + item.updated + '</td><td><div class="ui-row-actions"><button type="button" data-edit title="编辑"><i class="bi bi-pencil"></i></button><button type="button" data-delete title="删除"><i class="bi bi-trash3"></i></button></div></td>';
      row.querySelector('input').addEventListener('click', event => { event.stopPropagation(); event.target.checked ? selected.add(item.id) : selected.delete(item.id); updateSelection(); });
      row.querySelector('[data-edit]').addEventListener('click', event => { event.stopPropagation(); openForm(item); });
      row.querySelector('[data-delete]').addEventListener('click', event => { event.stopPropagation(); if (confirm('确认删除“' + item.name + '”？')) { records = records.filter(record => record.id !== item.id); selected.delete(item.id); render(); notify('记录已删除'); } });
      row.addEventListener('click', () => openDetail(item));
      body.appendChild(row);
    });
    find('[data-empty]').hidden = items.length !== 0;
    find('[data-result-summary]').textContent = '共 ' + items.length + ' 条记录';
    find('[data-page-summary]').textContent = page + ' / ' + pages;
    find('[data-prev]').disabled = page <= 1;
    find('[data-next]').disabled = page >= pages;
    updateSelection();
  }
  function openForm(item) {
    const form = find('[data-form]');
    form.classList.remove('was-validated');
    find('[data-form-title]').textContent = item ? '编辑记录' : '新增记录';
    find('[data-id]').value = item ? item.id : '';
    find('[data-name]').value = item ? item.name : '';
    find('[data-category]').value = item ? item.category : '';
    find('[data-owner]').value = item ? item.owner : '';
    modal.show();
  }
  function openDetail(item) {
    activeId = item.id;
    find('[data-detail-title]').textContent = item.name;
    find('[data-detail-body]').innerHTML = '<div class="ui-detail-hero"><small class="text-secondary">' + item.code + '</small><strong>' + escapeHtml(item.name) + '</strong></div><div class="ui-detail-grid"><div class="ui-detail-item"><span>分类</span><strong>' + item.category + '</strong></div><div class="ui-detail-item"><span>负责人</span><strong>' + escapeHtml(item.owner) + '</strong></div><div class="ui-detail-item"><span>状态</span><strong>' + item.status + '</strong></div><div class="ui-detail-item"><span>更新时间</span><strong>' + item.updated + '</strong></div></div>';
    detail.show();
  }
  find('[data-create]').addEventListener('click', () => openForm());
  find('[data-edit-detail]').addEventListener('click', () => { const item = records.find(record => record.id === activeId); detail.hide(); if (item) openForm(item); });
  find('[data-form]').addEventListener('submit', event => {
    event.preventDefault();
    const form = event.currentTarget;
    if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
    const id = Number(find('[data-id]').value);
    const existing = records.find(item => item.id === id);
    const data = { name: find('[data-name]').value.trim(), category: find('[data-category]').value, owner: find('[data-owner]').value.trim() };
    if (existing) Object.assign(existing, data, { updated: '刚刚' });
    else records.unshift({ id: Date.now(), code: 'DEMO-' + String(records.length + 1).padStart(3, '0'), status: '启用', updated: '刚刚', ...data });
    modal.hide(); page = 1; render(); notify(existing ? '记录已更新' : '记录已创建');
  });
  find('[data-search]').addEventListener('input', () => { page = 1; render(); });
  find('[data-status]').addEventListener('change', () => { page = 1; render(); });
  find('[data-reset]').addEventListener('click', () => { find('[data-search]').value = ''; find('[data-status]').value = ''; page = 1; render(); });
  find('[data-check-all]').addEventListener('change', event => { filtered().slice((page - 1) * pageSize, page * pageSize).forEach(item => event.target.checked ? selected.add(item.id) : selected.delete(item.id)); render(); });
  find('[data-batch]').addEventListener('click', () => { records.forEach(item => { if (selected.has(item.id)) item.status = '停用'; }); selected.clear(); render(); notify('批量操作已完成'); });
  find('[data-prev]').addEventListener('click', () => { if (page > 1) { page--; render(); } });
  find('[data-next]').addEventListener('click', () => { if (page * pageSize < filtered().length) { page++; render(); } });
  document.querySelector('[href="#recordModal"]')?.addEventListener('click', event => { event.preventDefault(); openForm(); });
  render();
})();