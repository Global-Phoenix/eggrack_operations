(() => {
    const root = document.querySelector('[data-workspace-tabs]');
    if (!root) return;

    const storageKey = 'eggrack.operations.workspace-tabs.v1';
    const viewport = root.querySelector('[data-workspace-viewport]');
    const list = root.querySelector('[data-workspace-list]');
    const contextMenu = root.querySelector('[data-workspace-context-menu]');
    const homeTab = { key: 'overview', title: '概览', url: '/', isClosable: false, icon: 'bi-house-door' };
    let contextKey = null;

    const safeUrl = value => {
        try {
            const parsed = new URL(value || '/', window.location.origin);
            if (parsed.origin !== window.location.origin || parsed.username || parsed.password) return null;
            return parsed.pathname + parsed.search + parsed.hash;
        } catch {
            return null;
        }
    };

    const cleanText = (value, fallback, maxLength = 80) => {
        const text = String(value || '').trim();
        return (text || fallback).slice(0, maxLength);
    };

    const sanitizeTab = raw => {
        if (!raw || typeof raw !== 'object') return null;
        const key = cleanText(raw.key, '', 120);
        const url = safeUrl(raw.url);
        if (!key || !url) return null;
        const icon = /^bi-[a-z0-9-]+$/i.test(raw.icon || '') ? raw.icon : null;
        return {
            key,
            title: cleanText(raw.title, '工作台'),
            url,
            isClosable: key === homeTab.key ? false : raw.isClosable !== false,
            icon
        };
    };

    const loadState = () => {
        try {
            const stored = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
            if (!stored || stored.version !== 1 || !Array.isArray(stored.tabs)) throw new Error('invalid workspace state');
            const seen = new Set();
            const tabs = stored.tabs.map(sanitizeTab).filter(tab => tab && !seen.has(tab.key) && seen.add(tab.key)).slice(0, 40);
            const withoutHome = tabs.filter(tab => tab.key !== homeTab.key);
            return { version: 1, tabs: [homeTab, ...withoutHome], activeKey: cleanText(stored.activeKey, homeTab.key, 120) };
        } catch {
            return { version: 1, tabs: [homeTab], activeKey: homeTab.key };
        }
    };

    const currentTab = sanitizeTab({
        key: root.dataset.currentKey,
        title: root.dataset.currentTitle,
        url: root.dataset.currentUrl,
        isClosable: root.dataset.currentClosable === 'true',
        icon: root.dataset.currentIcon
    }) || homeTab;

    const state = loadState();
    const currentIndex = state.tabs.findIndex(tab => tab.key === currentTab.key);
    if (currentIndex >= 0) state.tabs[currentIndex] = currentTab;
    else state.tabs.push(currentTab);
    state.activeKey = currentTab.key;

    const persist = () => {
        try {
            sessionStorage.setItem(storageKey, JSON.stringify(state));
        } catch {
            // The workspace still works for this page when browser storage is unavailable.
        }
    };

    const updateDocumentTitle = tab => {
        if (tab?.key === state.activeKey) document.title = tab.title + ' - EggRack Operations';
    };

    const updateOverflow = () => {
        if (!viewport) return;
        const hasOverflow = viewport.scrollWidth > viewport.clientWidth + 2;
        root.querySelectorAll('[data-workspace-scroll]').forEach(button => { button.hidden = !hasOverflow; });
        const left = root.querySelector('[data-workspace-scroll="left"]');
        const right = root.querySelector('[data-workspace-scroll="right"]');
        if (left) left.disabled = viewport.scrollLeft <= 1;
        if (right) right.disabled = viewport.scrollLeft + viewport.clientWidth >= viewport.scrollWidth - 1;
    };

    const navigate = tab => {
        if (!tab) return;
        state.activeKey = tab.key;
        persist();
        window.location.assign(tab.url);
    };

    const closeTab = key => {
        const index = state.tabs.findIndex(tab => tab.key === key);
        if (index < 0 || !state.tabs[index].isClosable) return;
        const wasActive = state.activeKey === key;
        state.tabs.splice(index, 1);
        if (wasActive) {
            const replacement = state.tabs[Math.max(0, index - 1)] || homeTab;
            state.activeKey = replacement.key;
            persist();
            navigate(replacement);
            return;
        }
        persist();
        render();
    };

    const applyCommand = (command, targetKey = state.activeKey) => {
        const index = state.tabs.findIndex(tab => tab.key === targetKey);
        if (index < 0) return;
        const target = state.tabs[index];
        if (command === 'refresh') {
            if (target.key === currentTab.key) window.location.reload();
            else navigate(target);
            return;
        }
        if (command === 'close' || command === 'close-current') {
            closeTab(target.key);
            return;
        }

        if (command === 'close-others') {
            state.tabs = state.tabs.filter(tab => !tab.isClosable || tab.key === target.key);
        } else if (command === 'close-left') {
            state.tabs = state.tabs.filter((tab, tabIndex) => !tab.isClosable || tabIndex >= index);
        } else if (command === 'close-right') {
            state.tabs = state.tabs.filter((tab, tabIndex) => !tab.isClosable || tabIndex <= index);
        } else if (command === 'close-all') {
            state.tabs = [homeTab];
        }

        const active = state.tabs.find(tab => tab.key === state.activeKey);
        if (!active) {
            const replacement = state.tabs.find(tab => tab.key === target.key) || state.tabs[state.tabs.length - 1] || homeTab;
            state.activeKey = replacement.key;
            persist();
            navigate(replacement);
            return;
        }
        persist();
        render();
    };

    const hideContextMenu = () => {
        if (contextMenu) contextMenu.hidden = true;
        contextKey = null;
    };

    const showContextMenu = (event, tab) => {
        if (!contextMenu) return;
        event.preventDefault();
        contextKey = tab.key;
        const closeButton = contextMenu.querySelector('[data-workspace-context-command="close"]');
        if (closeButton) closeButton.disabled = !tab.isClosable;
        contextMenu.hidden = false;
        const width = contextMenu.offsetWidth;
        const height = contextMenu.offsetHeight;
        contextMenu.style.left = Math.max(8, Math.min(event.clientX, window.innerWidth - width - 8)) + 'px';
        contextMenu.style.top = Math.max(8, Math.min(event.clientY, window.innerHeight - height - 8)) + 'px';
    };

    function render() {
        if (!list) return;
        list.replaceChildren();
        state.tabs.forEach(tab => {
            const item = document.createElement('div');
            item.className = 'workspace-tab' + (tab.key === state.activeKey ? ' is-active' : '');
            item.dataset.workspaceKey = tab.key;

            const main = document.createElement('button');
            main.type = 'button';
            main.className = 'workspace-tab-main';
            main.setAttribute('role', 'tab');
            main.setAttribute('aria-selected', String(tab.key === state.activeKey));
            main.title = tab.title;
            if (tab.icon) {
                const icon = document.createElement('i');
                icon.className = 'bi ' + tab.icon;
                icon.setAttribute('aria-hidden', 'true');
                main.append(icon);
            }
            const label = document.createElement('span');
            label.textContent = tab.title;
            main.append(label);
            main.addEventListener('click', () => navigate(tab));
            item.append(main);

            if (tab.isClosable) {
                const close = document.createElement('button');
                close.type = 'button';
                close.className = 'workspace-tab-close';
                close.title = '关闭 ' + tab.title;
                close.setAttribute('aria-label', '关闭 ' + tab.title);
                close.innerHTML = '<i class="bi bi-x" aria-hidden="true"></i>';
                close.addEventListener('click', event => {
                    event.stopPropagation();
                    closeTab(tab.key);
                });
                item.append(close);
            }

            item.addEventListener('auxclick', event => {
                if (event.button === 1 && tab.isClosable) {
                    event.preventDefault();
                    closeTab(tab.key);
                }
            });
            item.addEventListener('contextmenu', event => showContextMenu(event, tab));
            list.append(item);
        });

        updateDocumentTitle(state.tabs.find(tab => tab.key === state.activeKey));
        requestAnimationFrame(() => {
            list.querySelector('.workspace-tab.is-active')?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
            updateOverflow();
        });
    }

    root.querySelectorAll('[data-workspace-scroll]').forEach(button => {
        button.addEventListener('click', () => {
            viewport?.scrollBy({ left: button.dataset.workspaceScroll === 'left' ? -280 : 280, behavior: 'smooth' });
        });
    });
    root.querySelectorAll('[data-workspace-command]').forEach(button => {
        button.addEventListener('click', () => applyCommand(button.dataset.workspaceCommand));
    });
    contextMenu?.querySelectorAll('[data-workspace-context-command]').forEach(button => {
        button.addEventListener('click', () => {
            const command = button.dataset.workspaceContextCommand;
            const key = contextKey;
            hideContextMenu();
            if (key) applyCommand(command, key);
        });
    });
    viewport?.addEventListener('scroll', updateOverflow, { passive: true });
    viewport?.addEventListener('wheel', event => {
        if (Math.abs(event.deltaY) <= Math.abs(event.deltaX) || viewport.scrollWidth <= viewport.clientWidth) return;
        event.preventDefault();
        viewport.scrollLeft += event.deltaY;
    }, { passive: false });
    document.addEventListener('click', event => {
        if (!event.target.closest('[data-workspace-context-menu]')) hideContextMenu();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') hideContextMenu();
    });
    document.querySelector('[data-workspace-reset]')?.addEventListener('submit', () => {
        try { sessionStorage.removeItem(storageKey); } catch { /* no-op */ }
    });
    window.addEventListener('resize', updateOverflow);
    if ('ResizeObserver' in window && viewport) new ResizeObserver(updateOverflow).observe(viewport);

    persist();
    render();
})();
