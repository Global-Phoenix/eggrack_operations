(() => {
    const root = document.querySelector('[data-sourcing]');
    if (!root || typeof bootstrap === 'undefined') return;
    const { request } = window.Eggrack.http;
    const { escapeHtml: esc, filterRows, setBusy, notify } = window.Eggrack.ui;
    const planInput = root.querySelector('[data-plan-id]');
    let workspace;
    const modal = selector => { const element = root.querySelector(selector); return element ? bootstrap.Modal.getOrCreateInstance(element) : null; };

    const fillSelects = () => {
        root.querySelectorAll('[data-plan-items]').forEach(select => {
            const old = select.value;
            const optional = select.querySelector('option[value=""]')?.outerHTML || '<option value="">选择计划产品</option>';
            select.innerHTML = optional + workspace.planItems.map(x => `<option value="${x.id}">${esc(x.productName)} · ${x.quantity} ${esc(x.unit)}</option>`).join('');
            select.value = old;
        });
        root.querySelectorAll('[data-suppliers-select]').forEach(select => {
            const optional = select.querySelector('option[value=""]')?.outerHTML || '<option value="">选择供应商</option>';
            const old = select.value;
            select.innerHTML = optional + workspace.suppliers.filter(x => x.status === 'active').map(x => `<option value="${x.id}">${esc(x.name)}</option>`).join('');
            select.value = old;
        });
        const sampleSelect = root.querySelector('[data-sample-select]');
        if (sampleSelect) {
            const old = sampleSelect.value;
            sampleSelect.innerHTML = '<option value="">关联整个采购计划</option>' + workspace.samples.map(x => `<option value="${x.id}">${esc(x.productName)} · ${esc(x.supplierName || '未指定供应商')}</option>`).join('');
            sampleSelect.value = old;
        }
        const fileTypeSelect = root.querySelector('[data-file-types]');
        if (fileTypeSelect) {
            const old = fileTypeSelect.value;
            fileTypeSelect.innerHTML = '<option value="">选择文件类型</option>' + workspace.fileTypes.map(x => `<option value="${x.id}">${esc(x.name)}${x.allowedExtensions ? `（${esc(x.allowedExtensions)}）` : ''}</option>`).join('');
            fileTypeSelect.value = old;
        }
        const inquirySelect = root.querySelector('[data-inquiry-select]');
        if (inquirySelect) {
            const old = inquirySelect.value;
            inquirySelect.innerHTML = '<option value="">不关联报价</option>' + workspace.inquiries.map(x => `<option value="${x.id}">${esc(x.productName)} · ${esc(x.supplierName)} · V${x.revisionNo}</option>`).join('');
            inquirySelect.value = old;
        }
    };
    const fillForm = (form, item) => {
        Object.entries(item).forEach(([key, value]) => {
            const input = form.elements.namedItem(key.charAt(0).toUpperCase() + key.slice(1));
            if (input) input.value = value ?? '';
        });
    };
    const renderList = (selector, items, template, formSelector, modalSelector) => {
        const host = root.querySelector(selector);
        if (!host) return;
        host.innerHTML = items.length ? items.map((item, index) => `<div class="supplier"><div class="flex-grow-1">${template(item)}</div><button class="btn btn-sm btn-outline-secondary" type="button" data-edit-index="${index}">编辑</button></div>`).join('') : '<div class="text-secondary small">暂无记录</div>';
        host.querySelectorAll('[data-edit-index]').forEach(button => button.addEventListener('click', () => { fillForm(root.querySelector(formSelector), items[Number(button.dataset.editIndex)]); modal(modalSelector)?.show(); }));
    };
    const renderRequest = () => {
        const host = root.querySelector('[data-request-context]');
        if (!host || !workspace.request) return;
        const x = workspace.request;
        host.innerHTML = `<div class="request-context-header"><div><strong>当前计划申请版本</strong><span>询价与核价均以此版本为准</span></div><b>V${x.versionNumber}</b></div><div class="request-overview mb-3"><div class="wide"><small>申请编号</small><strong>${esc(x.requestNumber)}</strong></div><div><small>客户</small><strong>${esc(x.customerName)}</strong></div><div><small>邮箱</small><strong>${esc(x.email)}</strong></div><div class="wide"><small>提交时间</small><strong>${new Date(x.submittedAtUtc).toLocaleString()}</strong></div><div><small>当前计划</small><strong>${esc(x.planNumber)}</strong></div><div><small>申请版本</small><strong>V${x.versionNumber}</strong></div></div><div data-request-context-detail></div>`;
        window.Eggrack.procurementRequestView.render(host.querySelector('[data-request-context-detail]'), x);
        const source = root.querySelector('[data-request-source-items]');
        if (source) source.innerHTML = '<option value="">不引用，手工录入</option>' + x.items.map(item => `<option value="${item.id}">${esc(item.productName)} · ${item.quantity} ${esc(item.unit)}</option>`).join('');
    };
    const resetPlanItemForm = () => { const form = root.querySelector('[data-plan-item-form]'); if (!form) return; form.reset(); form.elements.Id.value = ''; form.elements.RequestItemId.value = ''; const source = root.querySelector('[data-request-source-items]'); if (source) source.value = ''; };
    const renderPlanItems = () => {
        const host = root.querySelector('[data-plan-item-list]'); if (!host) return;
        host.innerHTML = workspace.planItems.length ? workspace.planItems.map((item, index) => `<div class="plan-item-row"><div><strong>${index + 1}. ${esc(item.productName)}</strong><small>${item.quantity} ${esc(item.unit)}${item.requestItemId ? ' · 引用申请项' : ' · 内部新增'}</small></div><div><button class="btn btn-sm btn-outline-secondary" type="button" data-edit-plan-item="${item.id}">编辑</button><button class="btn btn-sm btn-outline-danger" type="button" data-delete-plan-item="${item.id}">删除</button></div></div>`).join('') : '<div class="empty-plan-items"><i class="bi bi-box-seam"></i><span>尚未建立内部计划产品，请引用申请项或手工新增。</span></div>';
        host.querySelectorAll('[data-edit-plan-item]').forEach(button => button.addEventListener('click', () => fillForm(root.querySelector('[data-plan-item-form]'), workspace.planItems.find(x => String(x.id) === button.dataset.editPlanItem))));
    };
    const renderPlanSuppliers = () => {
        const host = root.querySelector('[data-plan-suppliers]');
        if (!host) return;
        const ids = new Set([...workspace.inquiries.map(x => x.supplierId), ...workspace.samples.map(x => x.supplierId).filter(Boolean), ...workspace.candidates.map(x => x.supplierId).filter(Boolean)]);
        const suppliers = workspace.suppliers.filter(x => ids.has(x.id));
        host.innerHTML = suppliers.length ? `<div class="plan-supplier-grid">${suppliers.map(x => `<article><div><strong>${esc(x.name)}</strong>${x.code ? `<small>${esc(x.code)}</small>` : ''}</div><dl><div><dt>联系人</dt><dd>${esc(x.contactName || '—')}</dd></div><div><dt>电话</dt><dd>${esc(x.contactPhone || '—')}</dd></div><div><dt>地址</dt><dd>${esc(x.address || '—')}</dd></div><div><dt>法人</dt><dd>${esc(x.legalRepresentative || '—')}</dd></div></dl>${x.website ? `<a href="${esc(x.website)}" target="_blank" rel="noopener">访问网站</a>` : ''}</article>`).join('')}</div>` : '<div class="text-secondary small">保存第一条供应商报价后，相关供应商会显示在这里。</div>';
    };
    const filePreview = file => {
        const url = `/files/content/plan/${file.id}`;
        const mime = String(file.mimeType || '').toLowerCase();
        const planItem = workspace.planItems.find(x => x.id === file.planItemId);
        const supplier = workspace.suppliers.find(x => x.id === file.supplierId);
        const inquiry = workspace.inquiries.find(x => x.id === file.inquiryId);
        const visibility = { internal: '仅内部', supplier: '供应商沟通', customer: '可发送客户' }[file.visibility] || '仅内部';
        const relations = [planItem ? `产品：${planItem.productName}` : null, supplier ? `供应商：${supplier.name}` : null, inquiry ? `报价 V${inquiry.revisionNo}` : null, file.sampleId ? `样品 #${file.sampleId}` : null, visibility].filter(Boolean);
        const media = mime.startsWith('image/') ? `<img src="${url}" alt="${esc(file.originalName)}" loading="lazy">` : mime.startsWith('video/') ? `<video src="${url}" controls preload="metadata"></video>` : (mime === 'application/pdf' || mime.startsWith('text/')) ? `<iframe src="${url}" title="${esc(file.originalName)}" loading="lazy"></iframe>` : '<div class="text-secondary"><i class="bi bi-file-earmark"></i> 请下载查看</div>';
        return `<article class="sample-file-card"><div class="sample-file-preview">${media}</div><div class="sample-file-info"><div class="d-flex justify-content-between gap-2"><strong>${esc(file.originalName)}</strong><span class="stage">${esc(file.fileTypeName)}</span></div><p>${esc(file.description || '无文件说明')}</p><small>${file.sampleId ? `样品 #${file.sampleId}` : '计划公共文件'} · ${Math.max(1, Math.round(file.fileSize / 1024))} KB · ${new Date(file.uploadedAtUtc).toLocaleString()}</small><div class="mt-2"><a href="${url}" target="_blank" rel="noopener">预览</a><a href="${url}?download=true">下载</a></div></div></article>`;
    };
    const render = () => {
        renderRequest(); renderPlanItems(); fillSelects(); renderPlanSuppliers();
        renderList('[data-inquiries]', workspace.inquiries, x => `<div class="record-title"><strong>${esc(x.offeredProductName || x.productName)} <small>V${x.revisionNo}</small></strong><span class="stage ${x.isSelected ? 'text-bg-success' : ''}">${x.isSelected ? '已选中' : esc(x.status)}</span></div><div class="record-grid"><span><b>计划产品</b>${esc(x.productName)}</span><span><b>供应商</b>${esc(x.supplierName)}</span><span><b>报价</b>${esc(x.currency)} ${x.unitPrice ?? '—'}</span><span><b>MOQ / 交期</b>${x.moq ?? '—'} / ${x.leadDays ?? '—'} 天</span><span><b>尺寸</b>${[x.lengthCm, x.widthCm, x.heightCm].some(v => v != null) ? `${x.lengthCm ?? '—'} × ${x.widthCm ?? '—'} × ${x.heightCm ?? '—'} cm` : esc(x.sizeDetails || '—')}</span><span><b>重量 / 颜色</b>${x.weightKg ?? '—'} kg / ${esc(x.color || '—')}</span></div>${x.parameterDetails || x.terms || x.notes ? `<p class="record-note">${esc([x.parameterDetails, x.terms, x.notes].filter(Boolean).join(' · '))}</p>` : ''}${x.status === 'quoted' && !x.isSelected && workspace.planStatus === 2 ? `<button class="btn btn-sm btn-outline-success mt-2" type="button" data-select-inquiry="${x.id}" data-plan-item-id="${x.planItemId}">选为成本依据</button>` : ''}`, '[data-inquiry-form]', '#inquiryEditorModal');
        renderList('[data-samples]', workspace.samples, x => { const count = workspace.sampleFiles.filter(f => f.sampleId === x.id).length; return `<div class="record-title"><strong>${esc(x.productName)}</strong><span class="stage">${esc(x.status)}</span></div><div class="record-grid"><span><b>供应商</b>${esc(x.supplierName || '未指定')}</span><span><b>数量</b>${x.quantity}</span><span><b>费用</b>¥${Number(x.costCny).toFixed(2)}</span><span><b>附件</b>${count} 个</span><span><b>物流号</b>${esc(x.trackingNumber || '—')}</span><span><b>备注</b>${esc(x.notes || '—')}</span></div>`; }, '[data-sample-form]', '#sampleEditorModal');
        const files = root.querySelector('[data-sample-files]'); if (files) files.innerHTML = workspace.sampleFiles.length ? workspace.sampleFiles.map(filePreview).join('') : '<div class="text-secondary small">暂无样品文件。</div>';
        const history = root.querySelector('[data-review-history]');
        if (history) history.innerHTML = (workspace.quoteReviews || []).length ? workspace.quoteReviews.map(x => `<div class="supplier"><div><strong>${x.stage === 'department' ? '部门审核' : '最终审核'} · ${x.decision === 'recommended' ? '推荐' : x.decision === 'approved' ? '批准' : '退回'}</strong><br><small>${x.proposedQuoteUsd ? `USD ${Number(x.proposedQuoteUsd).toFixed(2)} · ` : ''}${new Date(x.actedAtUtc).toLocaleString()} · 操作人 #${x.actedBy}</small>${x.note ? `<br><small>${esc(x.note)}</small>` : ''}</div></div>`).join('') : '暂无审核记录。';
        root.querySelectorAll('[data-decision="review"]').forEach(x => x.classList.toggle('d-none', workspace.planStatus !== 3));
        root.querySelectorAll('[data-decision="approve"]').forEach(x => x.classList.toggle('d-none', workspace.planStatus !== 6));
        root.querySelectorAll('[data-decision="reject"]').forEach(x => x.classList.toggle('d-none', ![3, 6].includes(workspace.planStatus)));
        const pi = workspace.proformaInvoice; const piSummary = root.querySelector('[data-pi-summary]');
        piSummary.textContent = pi ? `${pi.number} · ${pi.currency} ${Number(pi.totalAmount).toFixed(2)} · ${pi.status}` : '批准报价后自动生成 PI 快照。';
        const pricing = workspace.proformaInvoicePricing; const piForm = root.querySelector('[data-pi-pricing-form]');
        const canEditPi = root.dataset.canManagePi === 'true' && pi && pi.status === 'Approved' && pricing;
        piForm?.classList.toggle('d-none', !canEditPi);
        if (piForm && pricing) {
            piForm.elements.InvoiceId.value = pricing.invoiceId;
            piForm.elements.PackagingFee.value = pricing.packagingFee; piForm.elements.ShippingFee.value = pricing.shippingFee; piForm.elements.OtherFee.value = pricing.otherFee; piForm.elements.DiscountAmount.value = pricing.discountAmount;
            piForm.querySelector('[data-pi-items]').innerHTML = pricing.items.map((x, index) => `<tr><td>${esc(x.productName)}<input type="hidden" name="Items[${index}].Id" value="${x.id}"></td><td>${x.quantity} ${esc(x.unit)}</td><td><input class="form-control form-control-sm" type="number" min="0" step="0.0001" name="Items[${index}].UnitPrice" value="${x.unitPrice}" required></td><td>USD ${Number(x.lineAmount).toFixed(2)}</td></tr>`).join('');
            piForm.querySelector('[data-pi-total]').textContent = `当前 PI 总额 USD ${Number(pricing.totalAmount).toFixed(2)}`;
        }
        root.querySelector('[data-pi-action="issue"]').classList.toggle('d-none', root.dataset.canIssuePi !== 'true' || !pi || pi.status !== 'Approved');
        root.querySelector('[data-pi-action="complete"]').classList.toggle('d-none', !pi || pi.status !== 'Issued' || !workspace.mailTasks.some(x => x.status === 'sent'));
        const mail = root.querySelector('[data-mail-tasks]');
        mail.innerHTML = workspace.mailTasks.length ? workspace.mailTasks.map(x => `<div class="supplier"><div><strong>${esc(x.recipient)}</strong><br><small>${esc(x.templateCode)} · ${new Date(x.createdAtUtc).toLocaleString()} · 尝试 ${x.attempts} 次</small>${x.lastError ? `<br><small class="text-danger">${esc(x.lastError)}</small>` : ''}</div><div><span class="stage">${esc(x.status)}</span>${pi && pi.status === 'Issued' ? ` <a class="btn btn-sm btn-outline-secondary" target="_blank" rel="noopener" href="/wholesale/procurement/mail-tasks/${x.id}/preview">预览</a>` : ''}${root.dataset.canSendMail === 'true' && pi && pi.status === 'Issued' && ['pending', 'failed'].includes(x.status) ? ` <button class="btn btn-sm btn-outline-primary" type="button" data-send-mail="${x.id}">${x.status === 'failed' ? '重试' : '发送'}</button>` : ''}</div></div>`).join('') : '暂无邮件任务。';
    };
    const loadWorkspace = async () => { workspace = await request(`/wholesale/procurement/plans/${planInput.value}/workspace`); render(); };
    const initialPlanId = root.dataset.initialPlanId;
    if (initialPlanId) { planInput.value = initialPlanId; loadWorkspace().catch(error => notify(error.message)); }
    root.querySelector('[data-search]')?.addEventListener('input', event => filterRows(root, event.target.value));
    const bindForm = (selector, url, modalSelector, successMessage) => root.querySelector(selector)?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(url, { method: 'POST', body: new FormData(event.target) }); event.target.reset(); const id = event.target.elements.namedItem('Id'); if (id) id.value = ''; modal(modalSelector)?.hide(); notify(successMessage, 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    bindForm('[data-inquiry-form]', '/wholesale/procurement/inquiry-records', '#inquiryEditorModal', '供应商报价已保存。'); bindForm('[data-sample-form]', '/wholesale/procurement/sample-records', '#sampleEditorModal', '样品记录已保存。');
    root.querySelector('[data-new-inquiry]')?.addEventListener('click', () => { const form = root.querySelector('[data-inquiry-form]'); form.reset(); form.elements.Id.value = ''; modal('#inquiryEditorModal')?.show(); });
    root.querySelector('[data-new-sample]')?.addEventListener('click', () => { const form = root.querySelector('[data-sample-form]'); form.reset(); form.elements.Id.value = ''; form.elements.Quantity.value = '1'; form.elements.CostCny.value = '0'; modal('#sampleEditorModal')?.show(); });
    root.querySelector('[data-request-source-items]')?.addEventListener('change', event => {
        const selectedId = event.target.value;
        resetPlanItemForm();
        const item = workspace.request.items.find(x => String(x.id) === selectedId); if (!item) return;
        const form = root.querySelector('[data-plan-item-form]'); form.elements.RequestItemId.value = item.id;
        ['ProductName','Quantity','Unit','Sku','Brand','Description','Specifications','Color','Size','PackagingRequirements','CustomizationRequirements'].forEach(name => { form.elements[name].value = item[name.charAt(0).toLowerCase() + name.slice(1)] ?? ''; });
        event.target.value = String(item.id);
    });
    root.querySelector('[data-new-plan-item]')?.addEventListener('click', () => { resetPlanItemForm(); root.querySelector('[data-plan-item-form]').scrollIntoView({ behavior: 'smooth', block: 'center' }); });
    root.querySelector('[data-reset-plan-item]')?.addEventListener('click', resetPlanItemForm);
    root.querySelector('[data-plan-item-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/items`, { method: 'POST', body: new FormData(event.target) }); resetPlanItemForm(); notify('计划产品已保存。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.addEventListener('click', async event => { const button = event.target.closest('[data-delete-plan-item]'); if (!button) return; if (!window.confirm('确定删除这个计划产品吗？')) return; setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/items/${button.dataset.deletePlanItem}/delete`, { method: 'POST', body: new FormData(root.querySelector('[data-plan-item-form]')) }); notify('计划产品已删除。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.addEventListener('click', async event => { const button = event.target.closest('[data-select-inquiry]'); if (!button) return; const body = new FormData(root.querySelector('[data-inquiry-form]')); body.set('PlanItemId', button.dataset.planItemId); body.set('InquiryId', button.dataset.selectInquiry); setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/inquiries/select`, { method: 'POST', body }); notify('已选为该产品的成本依据。', 'success'); await loadWorkspace(); if (costHost) await loadCosts(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.querySelector('[data-quick-supplier-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { const response = await request('/wholesale/procurement/suppliers', { method: 'POST', body: new FormData(event.target) }); await loadWorkspace(); const select = root.querySelector('[data-inquiry-form] [name="SupplierId"]'); if (select) select.value = String(response.id); event.target.reset(); modal('#supplierEditorModal')?.hide(); notify('供应商档案已保存。', 'success'); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.querySelector('[data-sample-file-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/sample-files`, { method: 'POST', body: new FormData(event.target) }); event.target.reset(); root.querySelector('[data-upload-file-name]').textContent = '尚未选择文件'; modal('#sampleFileModal')?.hide(); notify('样品文件已上传。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    const dropzone = root.querySelector('[data-upload-dropzone]'); const uploadInput = dropzone?.querySelector('input[type="file"]'); const fileName = root.querySelector('[data-upload-file-name]');
    const showFileName = () => { if (fileName) fileName.textContent = uploadInput?.files?.[0]?.name || '尚未选择文件'; };
    uploadInput?.addEventListener('change', showFileName);
    ['dragenter', 'dragover'].forEach(name => dropzone?.addEventListener(name, event => { event.preventDefault(); dropzone.classList.add('is-dragover'); }));
    ['dragleave', 'drop'].forEach(name => dropzone?.addEventListener(name, event => { event.preventDefault(); dropzone.classList.remove('is-dragover'); }));
    dropzone?.addEventListener('drop', event => { if (!uploadInput || !event.dataTransfer?.files?.length) return; uploadInput.files = event.dataTransfer.files; showFileName(); });

    const costHost = root.querySelector('[data-cost-items]');
    let costTypes = [];
    const addCostRow = (name = '', amount = 0, typeId = null) => { if (!costHost) return; const row = document.createElement('div'); row.className = 'cost-item-row'; row.innerHTML = `<select class="form-select" data-cost-type><option value="">自定义成本</option>${costTypes.map(x => `<option value="${x.id}"${String(x.id) === String(typeId) ? ' selected' : ''}>${esc(x.name)}</option>`).join('')}</select><input class="form-control" data-cost-name placeholder="自定义成本名称" value="${esc(name)}" required><input class="form-control" data-cost-amount type="number" min="0" step="0.01" value="${amount}" required><button class="btn btn-outline-danger" type="button" title="删除"><i class="bi bi-trash"></i></button>`; const select = row.querySelector('[data-cost-type]'); const nameInput = row.querySelector('[data-cost-name]'); const sync = () => { const type = costTypes.find(x => String(x.id) === select.value); if (type) { nameInput.value = type.name; nameInput.readOnly = true; } else nameInput.readOnly = false; }; select.addEventListener('change', sync); sync(); row.querySelector('button').addEventListener('click', () => row.remove()); costHost.append(row); };
    root.querySelector('[data-add-cost]')?.addEventListener('click', () => addCostRow());
    const loadCosts = async () => { if (!costHost) return; const costs = await request(`/wholesale/procurement/plans/${planInput.value}/costs`); costTypes = costs.types || []; costHost.replaceChildren(); const defaults = costTypes.slice(0, 3).map(x => ({ typeId: x.id, name: x.name, amountCny: 0 })); (costs.items.length ? costs.items : defaults).forEach(x => addCostRow(x.name, x.amountCny, x.typeId)); const form = root.querySelector('[data-cost-form]'); form.elements.CnyPerUsd.value = costs.cnyPerUsd; form.elements.ProfitRate.value = costs.profitRate; const products = root.querySelector('[data-product-costs]'); products.innerHTML = (costs.productCosts || []).length ? `<div class="table-responsive"><table class="table table-sm align-middle"><thead><tr><th>计划产品</th><th>供应商</th><th>报价</th><th>数量</th><th>折合 CNY</th></tr></thead><tbody>${costs.productCosts.map(x => `<tr><td>${esc(x.productName)}</td><td>${esc(x.supplierName)}</td><td>${esc(x.currency)} ${Number(x.unitPrice).toFixed(4)}</td><td>${x.quantity} ${esc(x.unit)}</td><td>¥${Number(x.amountCny).toFixed(2)}</td></tr>`).join('')}</tbody></table></div>` : '<div class="alert alert-warning mb-0">尚未为全部计划产品选择供应商报价。</div>'; const output = root.querySelector('[data-cost-result]'); if (costs.currentSnapshot) { output.textContent = `最近快照 V${costs.currentSnapshot.revisionNo} · ${costs.currentSnapshot.status} · 总成本 ¥${Number(costs.currentSnapshot.totalCostCny).toFixed(2)}`; output.className = 'alert alert-secondary mt-3 mb-0'; } };
    if (initialPlanId && root.dataset.canManageCosts === 'true') loadCosts().catch(error => notify(error.message));
    root.querySelector('[data-cost-form]')?.addEventListener('submit', async event => { event.preventDefault(); const rows = [...costHost.querySelectorAll('.cost-item-row')]; const body = new FormData(); body.append('__RequestVerificationToken', event.target.elements.__RequestVerificationToken.value); body.append('CnyPerUsd', event.target.elements.CnyPerUsd.value); body.append('ProfitRate', event.target.elements.ProfitRate.value); rows.forEach((row, index) => { body.append(`Items[${index}].TypeId`, row.querySelector('[data-cost-type]').value); body.append(`Items[${index}].Name`, row.querySelector('[data-cost-name]').value); body.append(`Items[${index}].AmountCny`, row.querySelector('[data-cost-amount]').value); }); const output = root.querySelector('[data-cost-result]'); try { const result = await request(`/wholesale/procurement/plans/${planInput.value}/costs`, { method: 'POST', body }); output.textContent = `总成本 ¥${result.data.totalCostCny.toFixed(2)}；成本 $${result.data.totalCostUsd.toFixed(2)}；建议报价 $${result.data.suggestedQuoteUsd.toFixed(2)}`; output.className = 'alert alert-info mt-3 mb-0'; } catch (error) { output.textContent = error.message; output.className = 'alert alert-danger mt-3 mb-0'; } });
    root.querySelector('[data-submit-costs]')?.addEventListener('click', async event => { const button = event.currentTarget; const form = root.querySelector('[data-cost-form]'); const body = new FormData(form); [...costHost.querySelectorAll('.cost-item-row')].forEach((row, index) => { body.append(`Items[${index}].TypeId`, row.querySelector('[data-cost-type]').value); body.append(`Items[${index}].Name`, row.querySelector('[data-cost-name]').value); body.append(`Items[${index}].AmountCny`, row.querySelector('[data-cost-amount]').value); }); setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/costs`, { method: 'POST', body }); const result = await request(`/wholesale/procurement/plans/${planInput.value}/costs/submit`, { method: 'POST', body: new FormData(form) }); notify(`成本快照 V${result.data.revisionNo} 已提交部门审核。`, 'success'); await loadWorkspace(); await loadCosts(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.querySelector('[data-approval-form]')?.addEventListener('submit', async event => { event.preventDefault(); const decision = event.submitter.dataset.decision; const output = root.querySelector('[data-approval-result]'); try { const result = await request(`/wholesale/procurement/plans/${planInput.value}/${decision}`, { method: 'POST', body: new FormData(event.target) }); output.textContent = decision === 'review' ? '部门审核完成，已提交老板终审。' : decision === 'approve' ? `最终报价已批准并生成 PI ${result.data.proformaInvoiceNumber}。` : '报价已退回采购修改。'; output.className = 'alert alert-success mt-3'; await loadWorkspace(); } catch (error) { output.textContent = error.message; output.className = 'alert alert-danger mt-3'; } });
    root.querySelector('[data-pi-pricing-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/pi/pricing`, { method: 'POST', body: new FormData(event.target) }); notify('PI 定价已保存。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.addEventListener('click', async event => { const button = event.target.closest('[data-send-mail]'); if (!button) return; setBusy(button, true); try { const response = await request(`/wholesale/procurement/mail-tasks/${button.dataset.sendMail}/send`, { method: 'POST', body: new FormData(root.querySelector('[data-approval-form]')) }); notify(response.message || '邮件发送成功。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); await loadWorkspace(); } finally { setBusy(button, false); } });
    root.querySelectorAll('[data-pi-action]').forEach(button => button.addEventListener('click', async () => { const action = button.dataset.piAction; setBusy(button, true); try { const suffix = action === 'issue' ? 'pi/issue' : 'complete'; await request(`/wholesale/procurement/plans/${planInput.value}/${suffix}`, { method: 'POST', body: new FormData(root.querySelector('[data-approval-form]')) }); notify(action === 'issue' ? 'PI 已签发。' : '采购计划已完成。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } }));
})();
