(() => {
    const root = document.querySelector('[data-procurement-plan-list]');
    if (!root) return;

    const search = root.querySelector('[data-plan-search]');
    const filters = [...root.querySelectorAll('[data-status]')];
    const rows = [...root.querySelectorAll('[data-plan-row]')];
    const empty = root.querySelector('[data-plan-empty]');
    let status = '';

    const render = () => {
        const keyword = (search.value || '').trim().toLowerCase();
        let visible = 0;
        rows.forEach(row => {
            const matchesStatus = !status || row.dataset.status === status;
            const matchesKeyword = !keyword || (row.dataset.search || '').toLowerCase().includes(keyword);
            const show = matchesStatus && matchesKeyword;
            row.hidden = !show;
            if (show) visible++;
        });
        empty.hidden = visible > 0 || rows.length === 0;
    };

    search.addEventListener('input', render);
    filters.forEach(button => button.addEventListener('click', () => {
        status = button.dataset.status || '';
        filters.forEach(item => item.classList.toggle('active', item === button));
        render();
    }));
})();
