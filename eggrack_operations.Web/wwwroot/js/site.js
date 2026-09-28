(() => {
    const shell = document.getElementById('adminShell');
    const mobile = document.getElementById('mobileMenu');
    const sidebarControl = document.getElementById('sidebarToggle');
    if (!shell) return;
    const toggleMobile = () => shell.classList.toggle('mobile-sidebar-open');
    mobile?.addEventListener('click', toggleMobile);
    sidebarControl?.addEventListener('click', toggleMobile);
    document.addEventListener('click', event => {
        if (window.innerWidth >= 768 || !shell.classList.contains('mobile-sidebar-open')) return;
        if (!event.target.closest('#sidebar') && !event.target.closest('#mobileMenu')) {
            shell.classList.remove('mobile-sidebar-open');
        }
    });
})();
