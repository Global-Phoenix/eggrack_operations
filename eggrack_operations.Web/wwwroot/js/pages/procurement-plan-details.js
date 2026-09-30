(() => {
    const root = document.querySelector('[data-procurement-plan-details]');
    if (!root) return;

    const loaded = new WeakSet();
    const loading = new WeakMap();

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

    async function loadPane(pane, force = false) {
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
        }).catch(error => renderFailure(pane, error.message)).finally(() => {
            pane.removeAttribute('aria-busy');
            loading.delete(pane);
        });
        loading.set(pane, request);
        return request;
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

    root.addEventListener('submit', async event => {
        const form = event.target.closest('[data-plan-supplier-form]');
        if (!form) return;
        event.preventDefault();
        const button = event.submitter;
        const errorBox = form.querySelector('[data-supplier-form-error]');
        errorBox?.classList.add('d-none');
        window.Eggrack.ui.setBusy(button, true);
        try {
            const response = await window.Eggrack.http.request(form.action, {
                method: 'POST',
                body: new FormData(form)
            });
            const modalElement = form.closest('.modal');
            const modal = modalElement && window.bootstrap?.Modal.getOrCreateInstance(modalElement);
            if (modalElement?.classList.contains('show')) {
                const hidden = new Promise(resolve => modalElement.addEventListener('hidden.bs.modal', resolve, { once: true }));
                modal.hide();
                await hidden;
            }
            window.Eggrack.ui.notify(response.message || '供应商已保存并关联当前计划。', 'success');
            const pane = form.closest('[data-plan-lazy-pane]');
            await loadPane(pane, true);
        } catch (error) {
            if (errorBox) {
                errorBox.textContent = error.message || '供应商保存失败。';
                errorBox.classList.remove('d-none');
            } else window.Eggrack.ui.notify(error.message || '供应商保存失败。');
        } finally {
            window.Eggrack.ui.setBusy(button, false);
        }
    });

    const requested = window.location.hash.slice(1);
    if (requested) {
        const button = root.querySelector(`[data-plan-tab="${CSS.escape(requested)}"]`);
        if (button && window.bootstrap?.Tab) window.bootstrap.Tab.getOrCreateInstance(button).show();
    }
})();
