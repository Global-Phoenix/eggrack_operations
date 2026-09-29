(() => {
    const root = document.querySelector('[data-purchase-requests]');
    if (!root || typeof bootstrap === 'undefined') return;
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

    const showError = message => {
        errorBox.textContent = message || '';
        errorBox.classList.toggle('d-none', !message);
    };
    const responseMessage = async response => {
        const type = response.headers.get('content-type') || '';
        if (type.includes('application/json')) return (await response.json()).message;
        const text = await response.text();
        return text || `请求失败（HTTP ${response.status}）`;
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
        versionSelect.disabled = true;
        versionSelect.innerHTML = '<option>正在加载版本…</option>';
        modal.show();
        try {
            const response = await fetch(`/wholesale/procurement/requests/${requestId}/versions`, { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error(await responseMessage(response));
            const versions = await response.json();
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
    root.querySelector('[data-search]')?.addEventListener('input', event => {
        const keyword = event.target.value.trim().toLowerCase();
        root.querySelectorAll('[data-row]').forEach(row => row.hidden = !row.textContent.toLowerCase().includes(keyword));
    });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (!versionSelect.value || !buyerSelect.value) return showError('请选择申请版本和采购人员。');
        submit.disabled = true;
        showError('');
        const body = new FormData(form);
        const planId = planInput.value;
        const url = planId ? `/wholesale/procurement/plans/${planId}` : '/wholesale/procurement/plans';
        try {
            const response = await fetch(url, { method: 'POST', body, headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error(await responseMessage(response));
            window.location.reload();
        } catch (error) {
            showError(error.message || '采购计划保存失败。');
            submit.disabled = false;
        }
    });
})();