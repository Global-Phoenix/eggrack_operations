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
        if (icon) icon.className = 'bi ' + (expanded ? 'bi-layout-sidebar-inset-reverse' : 'bi-layout-sidebar-inset');
        if (label) label.textContent = expanded ? '收起导航' : '展开导航';
        sidebarControl?.setAttribute('aria-expanded', String(expanded));
    };
    applySidebar(localStorage.getItem(sidebarStorageKey) === 'true');
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

    const fullscreenToggle = document.getElementById('fullscreenToggle');
    fullscreenToggle?.addEventListener('click', async () => {
        try {
            if (document.fullscreenElement) await document.exitFullscreen();
            else await document.documentElement.requestFullscreen();
        } catch { /* Browser may deny fullscreen without changing the page. */ }
    });
    const tabsHost = document.getElementById('workspaceTabs');
    const dynamicTabs = document.getElementById('dynamicTabs');
    if (!tabsHost || !dynamicTabs) return;

    const storageKey = 'eggrack.operations.tabs.v1';
    const currentPath = tabsHost.dataset.currentPath || '/';
    const currentTitle = tabsHost.dataset.currentTitle || '工作台';
    const homeTab = tabsHost.querySelector('[data-home-tab]');
    let tabs = [];
    try { tabs = JSON.parse(sessionStorage.getItem(storageKey) || '[]'); } catch { tabs = []; }
    tabs = tabs.filter(tab => tab && typeof tab.path === 'string' && tab.path !== '/');
    if (currentPath !== '/') {
        const existing = tabs.find(tab => tab.path === currentPath);
        if (existing) existing.title = currentTitle;
        else tabs.push({ path: currentPath, title: currentTitle });
    }
    const save = () => sessionStorage.setItem(storageKey, JSON.stringify(tabs));
    const render = () => {
        dynamicTabs.replaceChildren();
        homeTab?.classList.toggle('active', currentPath === '/');
        tabs.forEach((tab, index) => {
            const link = document.createElement('a');
            link.className = `workspace-tab${tab.path === currentPath ? ' active' : ''}`;
            link.href = tab.path;
            link.dataset.tabPath = tab.path;
            const label = document.createElement('span');
            label.textContent = tab.title;
            const close = document.createElement('button');
            close.type = 'button';
            close.className = 'tab-close';
            close.setAttribute('aria-label', `关闭 ${tab.title}`);
            close.textContent = '×';
            close.addEventListener('click', event => {
                event.preventDefault();
                event.stopPropagation();
                const wasCurrent = tab.path === currentPath;
                tabs = tabs.filter(item => item.path !== tab.path);
                save();
                if (wasCurrent) window.location.href = tabs[index - 1]?.path || tabs.at(-1)?.path || '/';
                else render();
            });
            link.append(label, close);
            dynamicTabs.append(link);
        });
    };
    save();
    render();
    tabsHost.querySelector('.new-tab-button')?.addEventListener('click', () => { window.location.href = '/'; });
    const removeTabs = paths => {
        const targets = new Set(paths.filter(path => path !== '/'));
        const currentIndex = tabs.findIndex(tab => tab.path === currentPath);
        const closesCurrent = targets.has(currentPath);
        tabs = tabs.filter(tab => !targets.has(tab.path));
        save();
        if (closesCurrent) window.location.href = tabs[Math.max(0, currentIndex - 1)]?.path || tabs.at(-1)?.path || '/';
        else render();
    };
    tabsHost.querySelectorAll('[data-tab-command]').forEach(button => button.addEventListener('click', () => {
        const command = button.dataset.tabCommand;
        if (command === 'refresh') return window.location.reload();
        const currentIndex = tabs.findIndex(tab => tab.path === currentPath);
        if (command === 'right') return removeTabs(tabs.slice(currentIndex + 1).map(tab => tab.path));
        if (command === 'other') return removeTabs(tabs.filter(tab => tab.path !== currentPath).map(tab => tab.path));
        if (command === 'all') return removeTabs(tabs.map(tab => tab.path));
    }));
})();

(() => {
    const shell = document.getElementById('adminShell');
    if (!shell) return;
    document.querySelectorAll('[data-nav-group]').forEach(button => {
        button.addEventListener('click', event => {
            if (window.innerWidth >= 768 && !shell.classList.contains('sidebar-expanded')) return;
            event.preventDefault();
            const group = button.closest('.nav-group');
            if (!group) return;
            const open = !group.classList.contains('open');
            document.querySelectorAll('.sidebar-expanded .nav-group.open').forEach(item => {
                if (item !== group) {
                    item.classList.remove('open');
                    item.querySelector('[data-nav-group]')?.setAttribute('aria-expanded', 'false');
                }
            });
            group.classList.toggle('open', open);
            button.setAttribute('aria-expanded', String(open));
        });
    });
})();
