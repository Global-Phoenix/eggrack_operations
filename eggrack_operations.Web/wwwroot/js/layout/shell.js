(() => {
    const shell = document.getElementById('adminShell');
    if (!shell) return;

    const mobile = document.getElementById('mobileMenu');
    const sidebarControl = document.getElementById('sidebarToggle');
    const sidebarStorageKey = 'eggrack.operations.sidebar.expanded';
    const toggleMobile = () => shell.classList.toggle('mobile-sidebar-open');
    const applySidebar = expanded => {
        shell.classList.toggle('sidebar-expanded', expanded);
        const icon = sidebarControl?.querySelector('[data-sidebar-icon]');
        const label = sidebarControl?.querySelector('span');
        if (icon) icon.className = `bi ${expanded ? 'bi-layout-sidebar-inset-reverse' : 'bi-layout-sidebar-inset'}`;
        if (label) label.textContent = expanded ? '收起导航' : '展开导航';
        sidebarControl?.setAttribute('aria-expanded', String(expanded));
    };

    const storedSidebarState = localStorage.getItem(sidebarStorageKey);
    applySidebar(storedSidebarState === null ? window.innerWidth >= 1024 : storedSidebarState === 'true');
    mobile?.addEventListener('click', toggleMobile);
    sidebarControl?.addEventListener('click', () => {
        if (window.innerWidth < 768) return toggleMobile();
        const expanded = !shell.classList.contains('sidebar-expanded');
        applySidebar(expanded);
        localStorage.setItem(sidebarStorageKey, String(expanded));
    });
    document.addEventListener('click', event => {
        if (window.innerWidth >= 768 || !shell.classList.contains('mobile-sidebar-open')) return;
        if (!event.target.closest('#sidebar') && !event.target.closest('#mobileMenu')) shell.classList.remove('mobile-sidebar-open');
    });

    document.getElementById('fullscreenToggle')?.addEventListener('click', async () => {
        try {
            if (document.fullscreenElement) await document.exitFullscreen();
            else await document.documentElement.requestFullscreen();
        } catch { /* Fullscreen may be denied without changing the page. */ }
    });
})();
