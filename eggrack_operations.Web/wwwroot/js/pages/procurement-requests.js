(() => {
    const root = document.querySelector('[data-purchase-requests]');
    if (!root || typeof bootstrap === 'undefined') return;
    const { request } = window.Eggrack.http;
    const { filterRows, setBusy, setMessage } = window.Eggrack.ui;
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

    const showError = message => setMessage(errorBox, message);
    const openEditor = async button => {
        showError('');
        const requestId = button.dataset.requestId;
        const planId = button.dataset.planId || '';
        requestInput.value = requestId;
        planInput.value = planId;
        buyerSelect.value = button.dataset.buyerId || '';
        title.textContent = planId ? '更新采购计划' : '创建采购计划';
        caption.textContent = planId ? `${button.dataset.number} 已有唯一计划；保存将按所选版本同步计划明细。` : `${button.dataset.number} 尚未创建计划。`;
        versionSelect.disabled = true;
        versionSelect.innerHTML = '<option>正在加载版本…</option>';
        modal.show();
        try {
            const versions = await request(`/wholesale/procurement/requests/${requestId}/versions`);
            versionSelect.replaceChildren(...versions.map(version => {
                const option = document.createElement('option');
                option.value = version.id;
                option.textContent = `V${version.versionNumber} · ${new Date(version.submittedAtUtc).toLocaleString()}`;
                option.selected = String(version.id) === button.dataset.versionId;
                return option;
            }));
            if (!versions.length) throw new Error('该采购申请没有可用版本。');
        } catch (error) {
            versionSelect.innerHTML = '<option value="">版本加载失败</option>';
            showError(error.message || '采购申请版本加载失败。');
        } finally {
            versionSelect.disabled = false;
        }
    };

    root.querySelectorAll('[data-edit-plan]').forEach(button => button.addEventListener('click', () => openEditor(button)));
    root.querySelector('.ui-page-actions .btn-primary')?.addEventListener('click', event => {
        const first = [...root.querySelectorAll('[data-edit-plan]')].find(button => !button.dataset.planId);
        if (!first) { event.preventDefault(); modal.hide(); window.alert('所有采购申请均已创建计划，请在对应申请行更新计划。'); return; }
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