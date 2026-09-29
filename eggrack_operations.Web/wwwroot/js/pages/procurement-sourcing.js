(() => {
    const root = document.querySelector('[data-sourcing]');
    if (!root || typeof bootstrap === 'undefined') return;
    const { request } = window.Eggrack.http;
    const { escapeHtml: esc, filterRows, setBusy, notify } = window.Eggrack.ui;
    const planInput = root.querySelector('[data-plan-id]');
    let workspace;

    const fillSelects = () => {
        root.querySelectorAll('[data-plan-items]').forEach(select => {
            const old = select.value;
            select.innerHTML = '<option value="">选择计划产品</option>' + workspace.planItems.map(x => `<option value="${x.id}">${esc(x.productName)} · ${x.quantity} ${esc(x.unit)}</option>`).join('');
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
    };
    const fillForm = (form, item) => {
        Object.entries(item).forEach(([key, value]) => {
            const input = form.elements.namedItem(key.charAt(0).toUpperCase() + key.slice(1));
            if (input) input.value = value ?? '';
        });
        form.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };
    const renderList = (selector, items, template, formSelector) => {
        const host = root.querySelector(selector);
        if (!host) return;
        host.innerHTML = items.length ? items.map((item, index) => `<div class="supplier"><div class="flex-grow-1">${template(item)}</div><button class="btn btn-sm btn-outline-secondary" type="button" data-edit-index="${index}">编辑</button></div>`).join('') : '<div class="text-secondary small">暂无记录</div>';
        host.querySelectorAll('[data-edit-index]').forEach(button => button.addEventListener('click', () => fillForm(root.querySelector(formSelector), items[Number(button.dataset.editIndex)])));
    };
    const renderRequest = () => {
        const host = root.querySelector('[data-request-context]');
        if (!host || !workspace.request) return;
        const x = workspace.request;
        host.innerHTML = `<div class="request-context-header"><div><strong>当前计划申请版本</strong><span>询价与核价均以此版本为准</span></div><b>V${x.versionNumber}</b></div><div class="request-overview mb-3"><div class="wide"><small>申请编号</small><strong>${esc(x.requestNumber)}</strong></div><div><small>客户</small><strong>${esc(x.customerName)}</strong></div><div><small>邮箱</small><strong>${esc(x.email)}</strong></div><div class="wide"><small>提交时间</small><strong>${new Date(x.submittedAtUtc).toLocaleString()}</strong></div><div><small>当前计划</small><strong>${esc(x.planNumber)}</strong></div><div><small>申请版本</small><strong>V${x.versionNumber}</strong></div></div><div data-request-context-detail></div>`;
        window.Eggrack.procurementRequestView.render(host.querySelector('[data-request-context-detail]'), x);
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
        const media = mime.startsWith('image/') ? `<img src="${url}" alt="${esc(file.originalName)}" loading="lazy">` : mime.startsWith('video/') ? `<video src="${url}" controls preload="metadata"></video>` : (mime === 'application/pdf' || mime.startsWith('text/')) ? `<iframe src="${url}" title="${esc(file.originalName)}" loading="lazy"></iframe>` : '<div class="text-secondary"><i class="bi bi-file-earmark"></i> 请下载查看</div>';
        return `<article class="sample-file-card"><div class="sample-file-preview">${media}</div><div class="sample-file-info"><strong>${esc(file.originalName)}</strong><p>${esc(file.description || '无文件说明')}</p><a href="${url}" target="_blank" rel="noopener">预览</a><a href="${url}?download=true">下载</a></div></article>`;
    };
    const render = () => {
        renderRequest(); fillSelects(); renderPlanSuppliers();
        renderList('[data-inquiries]', workspace.inquiries, x => `<strong>${esc(x.offeredProductName || x.productName)} / ${esc(x.supplierName)}</strong><br><small>${esc(x.currency)} ${x.unitPrice ?? '—'} · MOQ ${x.moq ?? '—'} · 交期 ${x.leadDays ?? '—'} 天 · ${esc(x.status)}</small><br><small>${[x.lengthCm, x.widthCm, x.heightCm].some(v => v != null) ? `${x.lengthCm ?? '—'} × ${x.widthCm ?? '—'} × ${x.heightCm ?? '—'} cm` : '未填尺寸'} · ${x.weightKg ?? '—'} kg · ${esc(x.color || '未填颜色')}</small>`, '[data-inquiry-form]');
        renderList('[data-samples]', workspace.samples, x => `<strong>${esc(x.productName)} / ${esc(x.supplierName || '未指定供应商')}</strong><br><small>${x.quantity} 件 · ¥${x.costCny} · ${esc(x.status)} · ${esc(x.trackingNumber || '无物流号')}</small>`, '[data-sample-form]');
        const files = root.querySelector('[data-sample-files]'); if (files) files.innerHTML = workspace.sampleFiles.length ? workspace.sampleFiles.map(filePreview).join('') : '<div class="text-secondary small">暂无样品文件。</div>';
        const pi = workspace.proformaInvoice; const piSummary = root.querySelector('[data-pi-summary]');
        piSummary.textContent = pi ? `${pi.number} · ${pi.currency} ${Number(pi.totalAmount).toFixed(2)} · ${pi.status}` : '批准报价后自动生成 PI 快照。';
        root.querySelector('[data-pi-action="issue"]').classList.toggle('d-none', !pi || pi.status !== 'Approved');
        root.querySelector('[data-pi-action="complete"]').classList.toggle('d-none', !pi || pi.status !== 'Issued' || !workspace.mailTasks.some(x => x.status === 'sent'));
        const mail = root.querySelector('[data-mail-tasks]');
        mail.innerHTML = workspace.mailTasks.length ? workspace.mailTasks.map(x => `<div class="supplier"><div><strong>${esc(x.recipient)}</strong><br><small>${esc(x.templateCode)} · ${new Date(x.createdAtUtc).toLocaleString()} · 尝试 ${x.attempts} 次</small>${x.lastError ? `<br><small class="text-danger">${esc(x.lastError)}</small>` : ''}</div><div><span class="stage">${esc(x.status)}</span>${pi && pi.status === 'Issued' ? ` <a class="btn btn-sm btn-outline-secondary" target="_blank" rel="noopener" href="/wholesale/procurement/mail-tasks/${x.id}/preview">预览</a>` : ''}${pi && pi.status === 'Issued' && ['pending', 'failed'].includes(x.status) ? ` <button class="btn btn-sm btn-outline-primary" type="button" data-send-mail="${x.id}">${x.status === 'failed' ? '重试' : '发送'}</button>` : ''}</div></div>`).join('') : '暂无邮件任务。';
    };
    const loadWorkspace = async () => { workspace = await request(`/wholesale/procurement/plans/${planInput.value}/workspace`); render(); };
    const initialPlanId = root.dataset.initialPlanId;
    if (initialPlanId) { planInput.value = initialPlanId; loadWorkspace().catch(error => notify(error.message)); }
    root.querySelector('[data-search]')?.addEventListener('input', event => filterRows(root, event.target.value));
    const bindForm = (selector, url) => root.querySelector(selector)?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(url, { method: 'POST', body: new FormData(event.target) }); event.target.reset(); const id = event.target.elements.namedItem('Id'); if (id) id.value = ''; await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    bindForm('[data-inquiry-form]', '/wholesale/procurement/inquiry-records'); bindForm('[data-sample-form]', '/wholesale/procurement/sample-records');
    root.querySelector('[data-quick-supplier-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { const response = await request('/wholesale/procurement/suppliers', { method: 'POST', body: new FormData(event.target) }); await loadWorkspace(); const select = root.querySelector('[data-inquiry-form] [name="SupplierId"]'); if (select) select.value = String(response.id); event.target.reset(); notify('供应商已保存并选中。', 'success'); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.querySelector('[data-sample-file-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/sample-files`, { method: 'POST', body: new FormData(event.target) }); event.target.reset(); notify('样品文件已上传。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });

    const costHost = root.querySelector('[data-cost-items]');
    const addCostRow = (name = '', amount = 0) => { if (!costHost) return; const row = document.createElement('div'); row.className = 'cost-item-row'; row.innerHTML = `<input class="form-control" placeholder="成本名称" value="${esc(name)}" required><input class="form-control" type="number" min="0" step="0.01" value="${amount}" required><button class="btn btn-outline-danger" type="button" title="删除"><i class="bi bi-trash"></i></button>`; row.querySelector('button').addEventListener('click', () => row.remove()); costHost.append(row); };
    root.querySelector('[data-add-cost]')?.addEventListener('click', () => addCostRow());
    const loadCosts = async () => { if (!costHost) return; const costs = await request(`/wholesale/procurement/plans/${planInput.value}/costs`); costHost.replaceChildren(); (costs.items.length ? costs.items : [{ name: '产品采购成本', amountCny: 0 }, { name: '包装成本', amountCny: 0 }, { name: '运输成本', amountCny: 0 }]).forEach(x => addCostRow(x.name, x.amountCny)); const form = root.querySelector('[data-cost-form]'); form.elements.CnyPerUsd.value = costs.cnyPerUsd; form.elements.ProfitRate.value = costs.profitRate; };
    if (initialPlanId && root.dataset.canManageCosts === 'true') loadCosts().catch(error => notify(error.message));
    root.querySelector('[data-cost-form]')?.addEventListener('submit', async event => { event.preventDefault(); const rows = [...costHost.querySelectorAll('.cost-item-row')]; const body = new FormData(); body.append('__RequestVerificationToken', event.target.elements.__RequestVerificationToken.value); body.append('CnyPerUsd', event.target.elements.CnyPerUsd.value); body.append('ProfitRate', event.target.elements.ProfitRate.value); rows.forEach((row, index) => { const inputs = row.querySelectorAll('input'); body.append(`Items[${index}].Name`, inputs[0].value); body.append(`Items[${index}].AmountCny`, inputs[1].value); }); const output = root.querySelector('[data-cost-result]'); try { const result = await request(`/wholesale/procurement/plans/${planInput.value}/costs`, { method: 'POST', body }); output.textContent = `总成本 ¥${result.data.totalCostCny.toFixed(2)}；成本 $${result.data.totalCostUsd.toFixed(2)}；建议报价 $${result.data.suggestedQuoteUsd.toFixed(2)}`; output.className = 'alert alert-info mt-3 mb-0'; } catch (error) { output.textContent = error.message; output.className = 'alert alert-danger mt-3 mb-0'; } });
    root.querySelector('[data-approval-form]')?.addEventListener('submit', async event => { event.preventDefault(); const decision = event.submitter.dataset.decision; const output = root.querySelector('[data-approval-result]'); try { const result = await request(`/wholesale/procurement/plans/${planInput.value}/${decision}`, { method: 'POST', body: new FormData(event.target) }); output.textContent = decision === 'approve' ? `已批准并生成 PI ${result.data.proformaInvoiceNumber}，邮件任务 #${result.data.mailTaskId} 已创建。` : '报价已退回。'; output.className = 'alert alert-success mt-3'; await loadWorkspace(); } catch (error) { output.textContent = error.message; output.className = 'alert alert-danger mt-3'; } });
    root.querySelector('[data-pi-pricing-form]')?.addEventListener('submit', async event => { event.preventDefault(); const button = event.submitter; setBusy(button, true); try { await request(`/wholesale/procurement/plans/${planInput.value}/pi/pricing`, { method: 'POST', body: new FormData(event.target) }); notify('PI 定价已保存。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } });
    root.addEventListener('click', async event => { const button = event.target.closest('[data-send-mail]'); if (!button) return; setBusy(button, true); try { const response = await request(`/wholesale/procurement/mail-tasks/${button.dataset.sendMail}/send`, { method: 'POST', body: new FormData(root.querySelector('[data-approval-form]')) }); notify(response.message || '邮件发送成功。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); await loadWorkspace(); } finally { setBusy(button, false); } });
    root.querySelectorAll('[data-pi-action]').forEach(button => button.addEventListener('click', async () => { const action = button.dataset.piAction; setBusy(button, true); try { const suffix = action === 'issue' ? 'pi/issue' : 'complete'; await request(`/wholesale/procurement/plans/${planInput.value}/${suffix}`, { method: 'POST', body: new FormData(root.querySelector('[data-approval-form]')) }); notify(action === 'issue' ? 'PI 已签发。' : '采购计划已完成。', 'success'); await loadWorkspace(); } catch (error) { notify(error.message); } finally { setBusy(button, false); } }));
})();
