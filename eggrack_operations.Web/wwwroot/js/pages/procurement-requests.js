(() => {
    const root = document.querySelector('[data-purchase-requests]');
    if (!root) return;
    const { filterRows } = window.Eggrack.ui;
    root.querySelector('[data-search]')?.addEventListener('input', event => filterRows(root, event.target.value));
})();
