(() => {
    const workspace = document.querySelector('[data-staff-workspace]');
    if (!workspace) return;

    const tabs = [...workspace.querySelectorAll('[data-staff-tab]')];
    const panels = [...workspace.querySelectorAll('[data-staff-panel]')];
    const storageKey = 'eggrack.security.staff.department';

    const activate = key => {
        const tab = tabs.find(item => item.dataset.staffTab === key) || tabs[0];
        if (!tab) return;
        tabs.forEach(item => {
            const selected = item === tab;
            item.classList.toggle('is-active', selected);
            item.setAttribute('aria-selected', String(selected));
            item.tabIndex = selected ? 0 : -1;
        });
        panels.forEach(panel => panel.hidden = panel.dataset.staffPanel !== tab.dataset.staffTab);
        try { sessionStorage.setItem(storageKey, tab.dataset.staffTab); } catch { }
    };

    tabs.forEach((tab, index) => {
        tab.addEventListener('click', () => activate(tab.dataset.staffTab));
        tab.addEventListener('keydown', event => {
            if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(event.key)) return;
            event.preventDefault();
            const direction = event.key === 'ArrowUp' || event.key === 'ArrowLeft' ? -1 : 1;
            const target = tabs[(index + direction + tabs.length) % tabs.length];
            activate(target.dataset.staffTab);
            target.focus();
        });
    });

    let preferred;
    try { preferred = sessionStorage.getItem(storageKey); } catch { }
    activate(preferred);
})();
