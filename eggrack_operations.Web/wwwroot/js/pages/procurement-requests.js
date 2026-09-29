(() => {
    const root = document.querySelector('[data-purchase-requests]');
    if (!root || typeof bootstrap === 'undefined') return;
    const { request } = window.Eggrack.http;
    const { filterRows, setBusy, setMessage, notify } = window.Eggrack.ui;
    const modalElement = document.getElementById('planEditor');
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const form = root.querySelector('[data-plan-form]');
    const requestInput = root.querySelector('[data-request-id]');
    const planInput = root.querySelector('[data-plan-id]');
    const versionSelect = root.querySelector('[data-version]');
    const buyerSelect = root.querySelector('[data-buyer]');
    const errorBox = root.querySelector('[data-error]');
    const submit = root.querySelector('[data-submit]');
    const title = document.getElementById('planEditorTitle');
    const caption = root.querySelector('[data-plan-caption]');
    const overview = {
        number: root.querySelector('[data-request-number]'),
        customer: root.querySelector('[data-request-customer]'),
        email: root.querySelector('[data-request-email]'),
        submitted: root.querySelector('[data-request-submitted]'),
        plan: root.querySelector('[data-request-plan]')
    };
    const versionSummary = root.querySelector('[data-version-summary]');
    const versionItems = root.querySelector('[data-version-items]');
    let loadedVersions = [];

    const showError = message => setMessage(errorBox, message);
    const displayDate = value => value ? new Date(value).toLocaleString() : '—';
    const renderVersionDetail = () => {
        const version = loadedVersions.find(item => String(item.id) === versionSelect.value);
        if (!version) {
            versionSummary.textContent = '';
            versionItems.className = 'text-secondary small';
            versionItems.textContent = loadedVersions.length ? '请选择申请版本。' : '暂无可用申请明细。';
            return;
        }

        const items = version.items || [];
        overview.customer.textContent = version.customerName || '—';
        overview.email.textContent = version.email || '—';
        overview.submitted.textContent = displayDate(version.submittedAtUtc);
        versionSummary.textContent = `V${version.versionNumber} · ${items.length} 项产品`;
        versionItems.className = 'request-detail-content';
        window.Eggrack.procurementRequestView.render(versionItems, version);
    };
    const openEditor = async button => {
        showError('');
        const requestId = button.dataset.requestId;
        const planId = button.dataset.planId || '';
        requestInput.value = requestId;
        planInput.value = planId;
        buyerSelect.value = button.dataset.buyerId || '';
        title.textContent = planId ? '更新采购计划' : '创建采购计划';
        caption.textContent = planId ? `${button.dataset.number} 已有唯一计划；保存将按所选版本同步计划明细。` : `${button.dataset.number} 尚未创建计划。`;
        overview.number.textContent = button.dataset.number || '—';
        overview.customer.textContent = button.dataset.customer || '—';
        overview.email.textContent = button.dataset.email || '—';
        overview.submitted.textContent = displayDate(button.dataset.submittedAt);
        overview.plan.textContent = button.dataset.planNumber || '未创建';
        loadedVersions = [];
        renderVersionDetail();
        versionSelect.disabled = true;
        versionSelect.innerHTML = '<option>正在加载版本…</option>';
        modal.show();
        try {
            const versions = await request(`/wholesale/procurement/requests/${requestId}/versions`);
            loadedVersions = versions;
            versionSelect.replaceChildren(...versions.map(version => {
                const option = document.createElement('option');
                option.value = version.id;
                option.textContent = `V${version.versionNumber} · ${new Date(version.submittedAtUtc).toLocaleString()}`;
                option.selected = String(version.id) === button.dataset.versionId;
                return option;
            }));
            if (!versions.length) throw new Error('该采购申请没有可用版本。');
            if (!versionSelect.value) versionSelect.selectedIndex = 0;
            renderVersionDetail();
        } catch (error) {
            loadedVersions = [];
            versionSelect.innerHTML = '<option value="">版本加载失败</option>';
            renderVersionDetail();
            showError(error.message || '采购申请版本加载失败。');
        } finally {
            versionSelect.disabled = false;
        }
    };

    versionSelect.addEventListener('change', renderVersionDetail);
    root.querySelectorAll('[data-edit-plan]').forEach(button => button.addEventListener('click', () => openEditor(button)));
    root.querySelector('.ui-page-actions .btn-primary')?.addEventListener('click', event => {
        const first = [...root.querySelectorAll('[data-edit-plan]')].find(button => !button.dataset.planId);
        if (!first) { event.preventDefault(); modal.hide(); notify('所有采购申请均已创建计划，请在对应申请行更新计划。','info'); return; }
        event.preventDefault();
        openEditor(first);
    });
    root.querySelector('[data-search]')?.addEventListener('input', event => filterRows(root, event.target.value));
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (!versionSelect.value || !buyerSelect.value) return showError('请选择申请版本和采购人员。');
        setBusy(submit, true);
        showError('');
        const body = new FormData(form);
        const planId = planInput.value;
        const url = planId ? `/wholesale/procurement/plans/${planId}` : '/wholesale/procurement/plans';
        try {
            await request(url, { method: 'POST', body });
            window.location.reload();
        } catch (error) {
            showError(error.message || '采购计划保存失败。');
            setBusy(submit, false);
        }
    });
})();
