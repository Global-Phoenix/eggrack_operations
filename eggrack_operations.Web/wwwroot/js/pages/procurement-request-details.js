(() => {
    document.querySelectorAll('[data-request-tab]').forEach(button => {
        button.addEventListener('click', () => {
            const target = button.dataset.requestTab;
            const tab = target ? document.querySelector(`.request-detail-tabs [data-bs-target="${target}"]`) : null;
            if (!tab || typeof bootstrap === 'undefined') return;
            bootstrap.Tab.getOrCreateInstance(tab).show();
            tab.focus({ preventScroll: true });
            document.querySelector('.request-detail-tabs')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    });
})();
