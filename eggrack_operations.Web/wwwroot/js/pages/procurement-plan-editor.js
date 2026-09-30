(() => {
    const root = document.querySelector('[data-plan-editor-page]');
    if (!root) return;

    const { request } = window.Eggrack.http;
    const { escapeHtml: esc, setBusy, setMessage, notify } = window.Eggrack.ui;
    const form = root.querySelector('[data-plan-form]');
    const versionSelect = root.querySelector('[data-version]');
    const buyerSelect = root.querySelector('[data-buyer]');
    const referenceSelect = root.querySelector('[data-reference-item]');
    const itemHost = root.querySelector('[data-plan-items-editor]');
    const errorBox = root.querySelector('[data-error]');
    const submit = root.querySelector('[data-submit]');
    const versionSummary = root.querySelector('[data-version-summary]');
    const versionItems = root.querySelector('[data-version-items]');
    const requestId = root.dataset.requestId;
    const planId = root.dataset.planId || '';
    let loadedVersions = [];
    let planItems = [];

    const showError = message => setMessage(errorBox, message);
    const field = (item, name) => item?.[name.charAt(0).toLowerCase() + name.slice(1)] ?? '';
    const currentVersion = () => loadedVersions.find(item => String(item.id) === versionSelect.value);

    const renderReferenceOptions = () => {
        const version = currentVersion();
        referenceSelect.innerHTML = '<option value="">选择申请产品作为预填参考</option>' +
            (version?.items || []).map(item => `<option value="${item.id}">${esc(item.productName)} · ${item.quantity} ${esc(item.unit)}</option>`).join('');
    };

    const renderVersionDetail = () => {
        const version = currentVersion();
        if (!version) {
            versionSummary.textContent = '';
            versionItems.className = 'text-secondary small';
            versionItems.textContent = loadedVersions.length ? '请选择申请版本。' : '暂无可用申请明细。';
            renderReferenceOptions();
            return;
        }
        root.querySelector('[data-request-customer]').textContent = version.customerName || '—';
        root.querySelector('[data-request-email]').textContent = version.email || '—';
        root.querySelector('[data-request-submitted]').textContent = new Date(version.submittedAtUtc).toLocaleString();
        versionSummary.textContent = `V${version.versionNumber} · ${(version.items || []).length} 项产品 · ${(version.attachments || []).length} 个附件`;
        versionItems.className = 'request-detail-content';
        window.Eggrack.procurementRequestView.render(versionItems, version);
        renderReferenceOptions();
    };

    const readItems = () => [...itemHost.querySelectorAll('[data-plan-item-row]')].map(row => {
        const result = {};
        row.querySelectorAll('[data-field]').forEach(input => result[input.dataset.field] = input.value);
        return result;
    });

    const renderItems = () => {
        itemHost.innerHTML = planItems.length ? planItems.map((item, index) => `<article class="plan-editor-item" data-plan-item-row>
          <input type="hidden" name="Items[${index}].Id" data-field="id" value="${esc(field(item, 'Id'))}">
          <input type="hidden" name="Items[${index}].RequestItemId" data-field="requestItemId" value="${esc(field(item, 'RequestItemId'))}">
          <div class="plan-editor-item-head"><strong>计划产品 ${index + 1}</strong><span>${field(item, 'RequestItemId') ? '由申请预填，已独立保存' : '内部录入'}</span><button class="btn btn-sm btn-outline-danger" type="button" data-remove-plan-item="${index}" aria-label="删除计划产品"><i class="bi bi-trash"></i></button></div>
          <div class="row g-2">
           <div class="col-lg-5 col-md-6"><label class="form-label">产品名称</label><input class="form-control" name="Items[${index}].ProductName" data-field="productName" value="${esc(field(item, 'ProductName'))}" required></div>
           <div class="col-lg-2 col-md-3"><label class="form-label">数量</label><input class="form-control" type="number" min="0.001" step="0.001" name="Items[${index}].Quantity" data-field="quantity" value="${esc(field(item, 'Quantity') || '1')}" required></div>
           <div class="col-lg-2 col-md-3"><label class="form-label">单位</label><input class="form-control" name="Items[${index}].Unit" data-field="unit" value="${esc(field(item, 'Unit') || 'pcs')}" required></div>
           <div class="col-lg-3 col-md-4"><label class="form-label">SKU</label><input class="form-control" name="Items[${index}].Sku" data-field="sku" value="${esc(field(item, 'Sku'))}"></div>
           <div class="col-lg-3 col-md-4"><label class="form-label">品牌</label><input class="form-control" name="Items[${index}].Brand" data-field="brand" value="${esc(field(item, 'Brand'))}"></div>
           <div class="col-lg-3 col-md-4"><label class="form-label">规格</label><input class="form-control" name="Items[${index}].Specifications" data-field="specifications" value="${esc(field(item, 'Specifications'))}"></div>
           <div class="col-lg-3 col-md-4"><label class="form-label">颜色</label><input class="form-control" name="Items[${index}].Color" data-field="color" value="${esc(field(item, 'Color'))}"></div>
           <div class="col-lg-3 col-md-4"><label class="form-label">尺寸</label><input class="form-control" name="Items[${index}].Size" data-field="size" value="${esc(field(item, 'Size'))}"></div>
          </div>
          <details class="mt-2"><summary>更多产品资料</summary><div class="row g-2 mt-1">
           <div class="col-lg-6"><label class="form-label">产品描述</label><textarea class="form-control" rows="2" name="Items[${index}].Description" data-field="description">${esc(field(item, 'Description'))}</textarea></div>
           <div class="col-lg-3 col-md-6"><label class="form-label">包装要求</label><textarea class="form-control" rows="2" name="Items[${index}].PackagingRequirements" data-field="packagingRequirements">${esc(field(item, 'PackagingRequirements'))}</textarea></div>
           <div class="col-lg-3 col-md-6"><label class="form-label">定制要求</label><textarea class="form-control" rows="2" name="Items[${index}].CustomizationRequirements" data-field="customizationRequirements">${esc(field(item, 'CustomizationRequirements'))}</textarea></div>
           <div class="col-12"><label class="form-label">产品内部备注</label><input class="form-control" name="Items[${index}].InternalNote" data-field="internalNote" value="${esc(field(item, 'InternalNote'))}"></div>
          </div></details>
        </article>`).join('') : '<div class="empty-plan-items"><i class="bi bi-box-seam"></i><span>尚未录入内部计划产品。</span></div>';
    };

    const addItem = item => {
        planItems = readItems();
        planItems.push(item || { quantity: 1, unit: 'pcs' });
        renderItems();
    };

    const fillPlan = editor => {
        form.elements.Priority.value = editor.priority || 'normal';
        buyerSelect.value = String(editor.assignedBuyerStaffId || '');
        form.elements.InternalNote.value = editor.internalNote || '';
        planItems = editor.items || [];
        renderItems();
    };

    const load = async () => {
        showError('');
        versionSelect.disabled = true;
        try {
            const [versions, editor] = await Promise.all([
                request(`/wholesale/procurement/requests/${requestId}/versions`),
                planId ? request(`/wholesale/procurement/plans/${planId}/editor`) : Promise.resolve(null)
            ]);
            loadedVersions = versions;
            versionSelect.replaceChildren(...versions.map(version => {
                const option = document.createElement('option');
                option.value = version.id;
                option.textContent = `V${version.versionNumber} · ${new Date(version.submittedAtUtc).toLocaleString()}`;
                return option;
            }));
            if (!versions.length) throw new Error('该采购申请没有可用版本。');
            versionSelect.value = String(editor?.requestVersionId || root.dataset.currentVersionId || versions[0].id);
            if (editor) fillPlan(editor);
            else {
                form.elements.Priority.value = 'normal';
                const selectedVersion = versions.find(version => String(version.id) === versionSelect.value);
                planItems = (selectedVersion?.items || []).map(sourceItem => ({
                    ...sourceItem,
                    id: '',
                    requestItemId: sourceItem.id,
                    internalNote: ''
                }));
                if (!planItems.length) planItems = [{ quantity: 1, unit: 'pcs' }];
                renderItems();
            }
            renderVersionDetail();
        } catch (error) {
            loadedVersions = [];
            versionSelect.innerHTML = '<option value="">版本加载失败</option>';
            renderVersionDetail();
            showError(error.message || '采购计划资料加载失败。');
        } finally {
            versionSelect.disabled = false;
        }
    };

    versionSelect.addEventListener('change', renderVersionDetail);
    root.querySelector('[data-add-plan-item]').addEventListener('click', () => addItem());
    root.querySelector('[data-add-reference-item]').addEventListener('click', () => {
        const source = currentVersion()?.items?.find(item => String(item.id) === referenceSelect.value);
        if (!source) return notify('请先选择一项申请产品。', 'info');
        addItem({ ...source, id: '', requestItemId: source.id, internalNote: '' });
    });
    itemHost.addEventListener('click', event => {
        const button = event.target.closest('[data-remove-plan-item]');
        if (!button) return;
        planItems = readItems();
        planItems.splice(Number(button.dataset.removePlanItem), 1);
        renderItems();
    });
    form.addEventListener('submit', event => {
        planItems = readItems();
        if (!versionSelect.value || !buyerSelect.value) {
            event.preventDefault();
            return showError('请选择参考申请版本和采购人员。');
        }
        if (!planItems.length) {
            event.preventDefault();
            return showError('请至少录入一个内部计划产品。');
        }
        if (planItems.some(item => !item.productName?.trim() || Number(item.quantity) <= 0 || !item.unit?.trim())) {
            event.preventDefault();
            return showError('计划产品必须填写名称、有效数量和单位。');
        }
        renderItems();
        setBusy(submit, true);
        showError('');
    });

    load();
})();
