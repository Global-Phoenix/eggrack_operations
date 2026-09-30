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

    const requested = window.location.hash.slice(1);
    if (requested) {
        const button = root.querySelector(`[data-plan-tab="${CSS.escape(requested)}"]`);
        if (button && window.bootstrap?.Tab) window.bootstrap.Tab.getOrCreateInstance(button).show();
    }
})();
