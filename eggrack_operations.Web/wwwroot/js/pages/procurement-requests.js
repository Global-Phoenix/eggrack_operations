(() => {
    const root = document.querySelector('[data-purchase-requests]');
    if (!root) return;
    const search = root.querySelector('[data-search]');
    const rowsHost = root.querySelector('[data-rows]');
    const rows = [...root.querySelectorAll('[data-row]')];
    const result = root.querySelector('[data-result-count]');
    const sort = root.querySelector('[data-sort]');
    const query = new URLSearchParams(window.location.search);
    let status = query.get('status') || 'all';
    const attention = query.get('attention');

    const apply = () => {
        const keyword = (search?.value || '').trim().toLocaleLowerCase();
        let visible = 0;
        rows.forEach(row => {
            const matchesStatus = status === 'all' || row.dataset.status === status;
            const matchesAttention = attention !== 'version-update' || row.dataset.versionOutdated === 'true';
            const matchesKeyword = !keyword || row.textContent.toLocaleLowerCase().includes(keyword);
            const show = matchesStatus && matchesAttention && matchesKeyword;
            row.hidden = !show;
            if (show) visible += 1;
        });
        if (result) result.textContent = `共 ${visible} 条当前筛选结果`;
    };

    const applySort = () => {
        if (!rowsHost) return;
        const mode = sort?.value || 'updated-desc';
        rows.sort((left, right) => {
            if (mode === 'updated-asc') return Number(left.dataset.updated) - Number(right.dataset.updated);
            if (mode === 'customer') return (left.dataset.customer || '').localeCompare(right.dataset.customer || '', 'zh-CN');
            if (mode === 'request') return (left.dataset.request || '').localeCompare(right.dataset.request || '', 'zh-CN');
            return Number(right.dataset.updated) - Number(left.dataset.updated);
        }).forEach(row => rowsHost.appendChild(row));
    };

    const buttons = [...root.querySelectorAll('[data-status-filter]')];
    if (!buttons.some(button => button.dataset.statusFilter === status)) status = 'all';
    buttons.forEach(button => {
        button.classList.toggle('active', button.dataset.statusFilter === status);
        button.addEventListener('click', () => {
            status = button.dataset.statusFilter || 'all';
            buttons.forEach(item => item.classList.toggle('active', item === button));
            apply();
        });
    });
    search?.addEventListener('input', apply);
    sort?.addEventListener('change', () => { applySort(); apply(); });
    applySort();
    apply();
})();
