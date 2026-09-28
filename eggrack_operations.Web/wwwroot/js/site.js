(() => {
    const shell = document.getElementById('adminShell');
    const toggle = document.getElementById('sidebarToggle');
    const mobile = document.getElementById('mobileMenu');
    const storageKey = 'eggrack.operations.sidebar.collapsed';
    if (!shell || !toggle) return;

    if (localStorage.getItem(storageKey) === 'true') shell.classList.add('sidebar-collapsed');
    toggle.addEventListener('click', () => {
        shell.classList.toggle('sidebar-collapsed');
        localStorage.setItem(storageKey, shell.classList.contains('sidebar-collapsed').toString());
    });
    mobile?.addEventListener('click', () => shell.classList.toggle('mobile-sidebar-open'));
    document.addEventListener('click', event => {
        if (window.innerWidth >= 992 || !shell.classList.contains('mobile-sidebar-open')) return;
        if (!event.target.closest('#sidebar') && !event.target.closest('#mobileMenu')) shell.classList.remove('mobile-sidebar-open');
    });
})();
