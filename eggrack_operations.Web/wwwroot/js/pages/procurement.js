(() => {
    const root = document.querySelector('[data-proc]');
    if (!root || typeof bootstrap === 'undefined') return;

    const drawer = bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('procurementDrawer'));
    const createModalElement = document.getElementById('createPlanModal');
    const createModal = bootstrap.Modal.getOrCreateInstance(createModalElement);
    const search = root.querySelector('[data-search]');
    const filter = root.querySelector('[data-filter]');
    const createButton = root.querySelector('.ui-page-actions .btn-primary[href="#"]');
    const createForm = root.querySelector('[data-create-form]');
    const requestSelect = root.querySelector('[data-request-select]');
    const createError = root.querySelector('[data-create-error]');
    const createSubmit = root.querySelector('[data-create-submit]');

    const filterRows = () => {
        const keyword = search.value.toLowerCase();
        root.querySelectorAll('[data-row]').forEach(row => {
            row.hidden = !(row.textContent.toLowerCase().includes(keyword) && (!filter.value || row.dataset.state === filter.value));
        });
    };
    search.addEventListener('input', filterRows);
    filter.addEventListener('change', filterRows);
    root.querySelector('[data-reset]')?.addEventListener('click', () => { search.value = ''; filter.value = ''; filterRows(); });
    root.querySelectorAll('[data-open]').forEach(button => button.addEventListener('click', () => {
        root.querySelector('[data-title]').textContent = button.dataset.plan;
        drawer.show();
    }));

    const showCreateError = message => {
        createError.textContent = message;
        createError.classList.toggle('d-none', !message);
    };
    const loadRequests = async () => {
        requestSelect.disabled = true;
        requestSelect.innerHTML = '<option value="">正在加载采购申请…</option>';
        showCreateError('');
        try {
            const response = await fetch('/wholesale/procurement/requests', { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error('采购申请加载失败，请稍后重试。');
            const requests = await response.json();
            requestSelect.innerHTML = '<option value="">请选择采购申请</option>';
            requests.forEach(item => {
                const option = document.createElement('option');
                option.value = `${item.id}|${item.currentVersionId}`;
                option.textContent = `${item.requestNumber} · V${item.currentVersion} · ${item.customerName}`;
                requestSelect.append(option);
            });
            if (!requests.length) showCreateError('当前没有可用于创建计划的采购申请。');
        } catch (error) {
            requestSelect.innerHTML = '<option value="">无法加载采购申请</option>';
            showCreateError(error.message || '采购申请加载失败。');
        } finally {
            requestSelect.disabled = false;
        }
    };
    createButton?.addEventListener('click', event => {
        event.preventDefault();
        createModal.show();
        loadRequests();
    });
    createForm?.addEventListener('submit', async event => {
        event.preventDefault();
        const selected = requestSelect.value.split('|');
        if (selected.length !== 2) return showCreateError('请选择采购申请。');
        createSubmit.disabled = true;
        showCreateError('');
        const body = new FormData(createForm);
        body.set('requestId', selected[0]);
        body.set('requestVersionId', selected[1]);
        try {
            const response = await fetch('/wholesale/procurement/plans', { method: 'POST', body });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || '采购计划创建失败。');
            window.location.reload();
        } catch (error) {
            showCreateError(error.message || '采购计划创建失败。');
            createSubmit.disabled = false;
        }
    });

    root.querySelector('[data-pricing]')?.addEventListener('submit', async event => {
        event.preventDefault();
        const response = await fetch('/wholesale/procurement/pricing', { method: 'POST', body: new FormData(event.target) });
        const result = await response.json();
        if (!response.ok) return window.alert(result.message);
        root.querySelector('[data-cny]').textContent = `¥${result.data.totalCostCny.toFixed(2)}`;
        root.querySelector('[data-usd]').textContent = `$${result.data.totalCostUsd.toFixed(2)}`;
        root.querySelector('[data-quote]').textContent = `$${result.data.suggestedQuoteUsd.toFixed(2)}`;
    });
})();