(() => {
    const root = document.querySelector('[data-procurement-plan-details]');
    if (!root) return;

    const loaded = new WeakSet();
    const loading = new WeakMap();
    const sourcingPane = () => root.querySelector('#planSourcing');
    const activeProductId = workspace => workspace?.querySelector('[data-sourcing-product-select]')?.value || '';
    const notify = (message, kind = 'danger') => window.Eggrack?.ui?.notify(message, kind);

    root.querySelectorAll('[data-plan-flash]').forEach(flash => notify(flash.dataset.message, flash.dataset.kind || 'success'));

    const renderFailure = (pane, message) => {
        pane.innerHTML = '';
        const box = document.createElement('div');
        box.className = 'plan-tab-load-error';
        const icon = document.createElement('i');
        icon.className = 'bi bi-exclamation-circle';
        const text = document.createElement('span');
        text.textContent = message || '内容加载失败，请稍后重试。';
        const retry = document.createElement('button');
        retry.type = 'button';
        retry.className = 'btn btn-sm btn-outline-primary';
        retry.textContent = '重试';
        retry.addEventListener('click', () => loadPane(pane, true));
        box.append(icon, text, retry);
        pane.append(box);
    };

    async function loadPane(pane, force = false, preferredProductId = '') {
        if (!pane || (!force && loaded.has(pane))) return;
        if (loading.has(pane)) return loading.get(pane);
        const url = pane.dataset.url;
        if (!url) return;
        pane.setAttribute('aria-busy', 'true');
        pane.innerHTML = '<div class="plan-tab-loading" role="status"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>正在加载业务数据…</span></div>';
        const request = fetch(url, {
            credentials: 'same-origin',
            headers: { 'Accept': 'text/html', 'X-Requested-With': 'XMLHttpRequest' }
        }).then(async response => {
            if (!response.ok) throw new Error(response.status === 403 ? '没有查看此业务内容的权限。' : '内容加载失败，请稍后重试。');
            pane.innerHTML = await response.text();
            loaded.add(pane);
            initializeFileUpload(pane);
            if (preferredProductId) setActiveProduct(pane.querySelector('[data-sourcing-workspace]'), preferredProductId);
        }).catch(error => renderFailure(pane, error.message)).finally(() => {
            pane.removeAttribute('aria-busy');
            loading.delete(pane);
        });
        loading.set(pane, request);
        return request;
    }

    function setActiveProduct(workspace, productId) {
        if (!workspace || !productId) return;
        const select = workspace.querySelector('[data-sourcing-product-select]');
        if (select && Array.from(select.options).some(option => option.value === productId)) select.value = productId;
        workspace.querySelectorAll('[data-sourcing-product-panel]').forEach(panel => {
            panel.classList.toggle('is-active', panel.dataset.sourcingProductPanel === productId);
        });
    }

    root.querySelectorAll('[data-plan-tab]').forEach(button => {
        button.addEventListener('show.bs.tab', () => {
            const selector = button.getAttribute('data-bs-target');
            const pane = selector ? root.querySelector(selector) : null;
            if (pane?.matches('[data-plan-lazy-pane]')) loadPane(pane);
        });
        button.addEventListener('shown.bs.tab', () => {
            const key = button.dataset.planTab || 'overview';
            history.replaceState(history.state, '', window.location.pathname + window.location.search + '#' + key);
        });
    });

    async function closeOverlay(form) {
        const modalElement = form.closest('.modal');
        if (modalElement?.classList.contains('show')) {
            const hidden = new Promise(resolve => modalElement.addEventListener('hidden.bs.modal', resolve, { once: true }));
            window.bootstrap?.Modal.getOrCreateInstance(modalElement).hide();
            await hidden;
        }
        const offcanvasElement = form.closest('.offcanvas');
        if (offcanvasElement?.classList.contains('show')) {
            const hidden = new Promise(resolve => offcanvasElement.addEventListener('hidden.bs.offcanvas', resolve, { once: true }));
            window.bootstrap?.Offcanvas.getOrCreateInstance(offcanvasElement).hide();
            await hidden;
        }
    }

    async function submitWorkspaceForm(form, button) {
        const errorBox = form.querySelector('[data-plan-form-error]');
        errorBox?.classList.add('d-none');
        window.Eggrack.ui.setBusy(button, true);
        const workspace = form.closest('[data-sourcing-workspace]');
        const productId = form.elements.PlanItemId?.value || activeProductId(workspace);
        try {
            const response = await window.Eggrack.http.request(form.action, { method: 'POST', body: new FormData(form) });
            await closeOverlay(form);
            notify(response.message || '操作已保存。', 'success');
            await loadPane(sourcingPane(), true, productId);
        } catch (error) {
            if (errorBox) {
                errorBox.textContent = error.message || '操作失败，请稍后重试。';
                errorBox.classList.remove('d-none');
            } else notify(error.message || '操作失败，请稍后重试。');
        } finally {
            window.Eggrack.ui.setBusy(button, false);
        }
    }

    async function selectInquiry(form, button) {
        const row = form.closest('[data-source-row]');
        const price = row?.dataset.unitPrice ? row.dataset.currency + ' ' + Number(row.dataset.unitPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 }) : '—';
        const quantity = Number(row?.dataset.quantity || 0);
        const unitPrice = Number(row?.dataset.unitPrice || 0);
        const estimated = unitPrice > 0 ? row.dataset.currency + ' ' + (quantity * unitPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—';
        const message = '确认选择此采购方案？\n\n供应商：' + (row?.dataset.supplierName || '—') + '\n产品：' + (row?.dataset.productName || '—') + '\n报价：' + price + ' / ' + (row?.dataset.unit || '件') + '\n数量：' + quantity.toLocaleString() + ' ' + (row?.dataset.unit || '') + '\n预计产品成本：' + estimated;
        if (!window.confirm(message)) return;
        window.Eggrack.ui.setBusy(button, true);
        try {
            const response = await window.Eggrack.http.request(form.action, { method: 'POST', body: new FormData(form) });
            updateProductProcurement(response);
            const compare = root.querySelector('#quoteCompareModal');
            if (compare?.classList.contains('show')) window.bootstrap?.Modal.getOrCreateInstance(compare).hide();
            notify(response.message || '采购方案已选择。', 'success');
            await loadPane(sourcingPane(), true, row?.dataset.planItemId || '');
        } catch (error) {
            notify(error.message || '采购方案选择失败。');
        } finally {
            window.Eggrack.ui.setBusy(button, false);
        }
    }

    function updateProductProcurement(response) {
        const product = root.querySelector('[data-product-procurement-item="' + CSS.escape(String(response.planItemId)) + '"]');
        if (!product) return;
        const set = (selector, value) => {
            const element = product.querySelector(selector);
            if (element) element.textContent = value || '—';
        };
        set('[data-selected-supplier]', response.selectedSupplierName);
        set('[data-selected-product]', response.selectedSupplierProductName);
        set('[data-selected-quote]', response.selectedSupplierUnitPrice == null ? '—' : response.selectedSupplierCurrency + ' ' + Number(response.selectedSupplierUnitPrice).toFixed(4));
        const price = product.querySelector('[data-purchase-unit-price]');
        if (price && response.purchaseUnitPriceCny != null) price.value = response.purchaseUnitPriceCny;
    }

    root.addEventListener('submit', async event => {
        const form = event.target.closest('[data-plan-supplier-form], [data-plan-candidate-form], [data-plan-inquiry-form], [data-plan-sample-form]');
        if (form) {
            event.preventDefault();
            await submitWorkspaceForm(form, event.submitter);
            return;
        }
        const selection = event.target.closest('[data-plan-select-inquiry]');
        if (selection) {
            event.preventDefault();
            await selectInquiry(selection, event.submitter);
        }
    });

    function openCandidateDrawer(trigger) {
        const workspace = trigger.closest('[data-sourcing-workspace]');
        const productId = trigger.dataset.planItemId || activeProductId(workspace);
        const panel = workspace.querySelector('[data-sourcing-product-panel="' + CSS.escape(productId) + '"]');
        const form = workspace.querySelector('[data-plan-candidate-form]');
        form.reset();
        form.elements.PlanItemId.value = productId;
        form.querySelector('[data-candidate-context]').textContent = panel ? (panel.querySelector('.sourcing-requirement-title strong')?.textContent || '') + ' · ' + (panel.querySelector('.sourcing-requirement-title b')?.textContent || '') : '';
        window.bootstrap?.Offcanvas.getOrCreateInstance(workspace.querySelector('#candidateDrawer')).show();
    }

    function setFormValue(form, name, value) {
        const input = form.elements[name];
        if (input) input.value = value || '';
    }

    function openQuoteDrawer(trigger) {
        const row = trigger.closest('[data-source-row]');
        const workspace = trigger.closest('[data-sourcing-workspace]');
        const form = workspace.querySelector('[data-plan-inquiry-form]');
        form.reset();
        const mapping = {
            Id: 'inquiryId', PlanItemId: 'planItemId', SupplierId: 'supplierId', OfferedProductName: 'productName',
            Currency: 'currency', UnitPrice: 'unitPrice', Moq: 'moq', LeadDays: 'leadDays', ValidUntil: 'validUntil',
            LengthCm: 'lengthCm', WidthCm: 'widthCm', HeightCm: 'heightCm', WeightKg: 'weightKg', Color: 'color',
            SizeDetails: 'sizeDetails', ParameterDetails: 'parameterDetails', Terms: 'terms', Notes: 'notes'
        };
        Object.entries(mapping).forEach(([name, dataKey]) => setFormValue(form, name, row.dataset[dataKey]));
        setFormValue(form, 'Status', 'quoted');
        form.querySelector('[data-quote-context]').textContent = (row.dataset.planItemName || '计划产品') + ' · ' + (row.dataset.supplierName || '供应商');
        window.bootstrap?.Offcanvas.getOrCreateInstance(workspace.querySelector('#quoteDrawer')).show();
    }

    function openSampleDrawer(trigger) {
        const workspace = trigger.closest('[data-sourcing-workspace]');
        const productId = trigger.dataset.planItemId || activeProductId(workspace);
        const panel = workspace.querySelector('[data-sourcing-product-panel="' + CSS.escape(productId) + '"]');
        const productName = trigger.dataset.planItemName || panel?.querySelector('.sourcing-requirement-title strong')?.textContent || '';
        const drawer = workspace.querySelector('#sampleDrawer');
        const form = drawer.querySelector('[data-plan-sample-form]');
        form.reset();
        form.elements.PlanItemId.value = productId;
        form.querySelector('[data-sample-context]').textContent = productName;
        let visible = 0;
        drawer.querySelectorAll('[data-sample-plan-item]').forEach(record => {
            const show = record.dataset.samplePlanItem === productId;
            record.hidden = !show;
            if (show) visible += 1;
        });
        drawer.querySelector('[data-sample-list]').classList.toggle('is-empty', visible === 0);
        window.bootstrap?.Offcanvas.getOrCreateInstance(drawer).show();
    }

    function openSupplierModal(trigger) {
        const workspace = trigger.closest('[data-sourcing-workspace]');
        const drawer = trigger.closest('.offcanvas');
        if (drawer?.classList.contains('show')) window.bootstrap?.Offcanvas.getOrCreateInstance(drawer).hide();
        window.bootstrap?.Modal.getOrCreateInstance(workspace.querySelector('#supplierModal')).show();
    }

    function buildComparison(trigger) {
        const workspace = trigger.closest('[data-sourcing-workspace]');
        const panel = workspace.querySelector('[data-sourcing-product-panel="' + CSS.escape(trigger.dataset.planItemId) + '"]');
        const rows = Array.from(panel.querySelectorAll('[data-source-row]')).filter(row => Number(row.dataset.unitPrice) > 0);
        const target = workspace.querySelector('[data-quote-comparison]');
        target.innerHTML = '';
        const table = document.createElement('table');
        table.className = 'table quote-comparison-table align-middle';
        const head = table.createTHead().insertRow();
        head.append(document.createElement('th'));
        rows.forEach(row => {
            const th = document.createElement('th');
            const strong = document.createElement('strong');
            strong.textContent = row.dataset.supplierName || '—';
            const span = document.createElement('span');
            span.textContent = row.dataset.productName || '—';
            th.append(strong, span);
            head.append(th);
        });
        const body = table.createTBody();
        const metrics = [
            ['采购单价', row => row.dataset.currency + ' ' + Number(row.dataset.unitPrice).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })],
            ['MOQ', row => row.dataset.moq || '—'],
            ['交期', row => row.dataset.leadDays ? row.dataset.leadDays + ' 天' : '—'],
            ['有效期', row => row.dataset.validUntil || '—'],
            ['采购数量', row => Number(row.dataset.quantity).toLocaleString() + ' ' + (row.dataset.unit || '')],
            ['预计产品金额', row => row.dataset.currency + ' ' + (Number(row.dataset.quantity) * Number(row.dataset.unitPrice)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })]
        ];
        metrics.forEach(metric => {
            const tr = body.insertRow();
            const title = tr.insertCell();
            title.textContent = metric[0];
            rows.forEach(row => {
                const cell = tr.insertCell();
                cell.textContent = metric[1](row);
            });
        });
        const action = body.insertRow();
        action.insertCell().textContent = '选择方案';
        rows.forEach(row => {
            const cell = action.insertCell();
            if (row.dataset.selected === 'true') {
                const selected = document.createElement('span');
                selected.className = 'ui-status ui-status-success';
                selected.textContent = '已选择';
                cell.append(selected);
                return;
            }
            const sourceForm = row.querySelector('[data-plan-select-inquiry]');
            if (!sourceForm) {
                cell.textContent = '—';
                return;
            }
            const form = sourceForm.cloneNode(true);
            const button = form.querySelector('button');
            button.className = 'btn btn-sm btn-primary';
            button.textContent = '选择 ' + row.dataset.supplierName;
            cell.append(form);
        });
        target.append(table);
        window.bootstrap?.Modal.getOrCreateInstance(workspace.querySelector('#quoteCompareModal')).show();
    }

    root.addEventListener('click', event => {
        const openTab = event.target.closest('[data-open-plan-tab]');
        if (openTab) {
            const tabButton = root.querySelector('[data-plan-tab="' + CSS.escape(openTab.dataset.openPlanTab) + '"]');
            if (tabButton) window.bootstrap?.Tab.getOrCreateInstance(tabButton).show();
            return;
        }
        const candidate = event.target.closest('[data-open-candidate-drawer]');
        if (candidate) return openCandidateDrawer(candidate);
        const quote = event.target.closest('[data-open-quote-drawer]');
        if (quote) return openQuoteDrawer(quote);
        const sample = event.target.closest('[data-open-sample-drawer]');
        if (sample) return openSampleDrawer(sample);
        const supplier = event.target.closest('[data-open-supplier-modal]');
        if (supplier) return openSupplierModal(supplier);
        const compare = event.target.closest('[data-open-compare]');
        if (compare) return buildComparison(compare);
        const filterButton = event.target.closest('[data-file-filter]');
        if (filterButton) {
            const toolbar = filterButton.closest('[data-plan-file-filters]');
            toolbar.dataset.activeFilter = filterButton.dataset.fileFilter;
            toolbar.querySelectorAll('[data-file-filter]').forEach(button => {
                const active = button === filterButton;
                button.classList.toggle('btn-primary', active);
                button.classList.toggle('active', active);
                button.classList.toggle('btn-outline-secondary', !active);
            });
            applyFileFilters(toolbar);
        }
    });

    root.addEventListener('input', event => {
        if (event.target.matches('[data-candidate-filter]')) {
            const query = event.target.value.trim().toLocaleLowerCase();
            event.target.closest('[data-sourcing-product-panel]').querySelectorAll('[data-source-row]').forEach(row => {
                row.hidden = query !== '' && !row.dataset.searchText.toLocaleLowerCase().includes(query);
            });
        }
        if (event.target.matches('[data-supplier-search]')) {
            const query = event.target.value.trim().toLocaleLowerCase();
            event.target.closest('[data-plan-candidate-form]').querySelectorAll('[data-supplier-option]').forEach(option => {
                option.hidden = query !== '' && !option.dataset.searchText.toLocaleLowerCase().includes(query);
            });
        }
    });

    root.addEventListener('change', event => {
        if (event.target.matches('[data-sourcing-product-select]')) setActiveProduct(event.target.closest('[data-sourcing-workspace]'), event.target.value);
        if (event.target.matches('[data-file-category]')) syncFileUploadRelations(event.target.closest('[data-plan-file-upload]'));
        const productFilter = event.target.closest('[data-file-product-filter]');
        if (productFilter) applyFileFilters(productFilter.closest('[data-plan-file-filters]'));
        const visibilityFilter = event.target.closest('[data-file-visibility-filter]');
        if (visibilityFilter) applyFileFilters(visibilityFilter.closest('[data-plan-file-filters]'));
    });

    function initializeFileUpload(scope) {
        scope?.querySelectorAll('[data-plan-file-upload]').forEach(syncFileUploadRelations);
    }

    function syncFileUploadRelations(form) {
        if (!form) return;
        const category = form.querySelector('[data-file-category]')?.value || 'product';
        const more = form.querySelector('[data-file-more-relations]');
        const visibleRelations = category === 'quote'
            ? new Set(['supplier', 'inquiry'])
            : category === 'sample'
                ? new Set(['supplier', 'sample'])
                : category === 'product'
                    ? new Set()
                    : new Set(['supplier', 'inquiry', 'sample']);
        form.querySelectorAll('[data-file-relation]').forEach(field => {
            const visible = visibleRelations.has(field.dataset.fileRelation);
            field.hidden = !visible;
            if (!visible) field.querySelectorAll('select,input').forEach(input => input.value = '');
        });
        if (more) {
            more.hidden = false;
            more.open = category === 'quote' || category === 'sample';
        }
    }

    function applyFileFilters(toolbar) {
        if (!toolbar) return;
        const pane = toolbar.closest('[data-plan-lazy-pane]');
        const category = toolbar.dataset.activeFilter || 'all';
        const visibility = toolbar.querySelector('[data-file-visibility-filter]')?.value || 'all';
        const product = toolbar.querySelector('[data-file-product-filter]')?.value || 'all';
        let visible = 0;
        pane.querySelectorAll('[data-plan-file-card]').forEach(card => {
            const matchesCategory = category === 'all' || card.dataset.fileCategory === category;
            const matchesVisibility = visibility === 'all' || card.dataset.fileVisibility === visibility;
            const matchesProduct = product === 'all' || card.dataset.planItem === product;
            const show = matchesCategory && matchesVisibility && matchesProduct;
            card.classList.toggle('d-none', !show);
            if (show) visible += 1;
        });
        pane.querySelector('[data-plan-file-empty]')?.classList.toggle('d-none', visible !== 0);
    }

    const requested = window.location.hash.slice(1);
    initializeFileUpload(root);
    if (requested) {
        const button = root.querySelector('[data-plan-tab="' + CSS.escape(requested) + '"]');
        if (button && window.bootstrap?.Tab) window.bootstrap.Tab.getOrCreateInstance(button).show();
    }
})();
