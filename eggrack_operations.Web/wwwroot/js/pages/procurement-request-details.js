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

    const fileWorkspace = document.querySelector('[data-file-preview-workspace]');
    if (!fileWorkspace) return;

    const fileButtons = Array.from(fileWorkspace.querySelectorAll('[data-file-preview-target]'));
    const filePanels = Array.from(fileWorkspace.querySelectorAll('[data-file-preview-panel]'));

    const selectFile = button => {
        const panelId = button.dataset.filePreviewTarget;
        const activePanel = panelId ? document.getElementById(panelId) : null;
        if (!activePanel || !fileWorkspace.contains(activePanel)) return;

        fileButtons.forEach(item => {
            const selected = item === button;
            item.classList.toggle('is-active', selected);
            item.setAttribute('aria-selected', selected ? 'true' : 'false');
            item.tabIndex = selected ? 0 : -1;
        });
        filePanels.forEach(panel => {
            const selected = panel === activePanel;
            panel.classList.toggle('is-active', selected);
            panel.setAttribute('aria-hidden', selected ? 'false' : 'true');
        });

        const preview = activePanel.querySelector('[data-file-preview-source][data-src]');
        if (preview && !preview.getAttribute('src')) {
            preview.setAttribute('src', preview.dataset.src);
            preview.removeAttribute('data-src');
        }
    };

    fileButtons.forEach((button, index) => {
        button.addEventListener('click', () => selectFile(button));
        button.addEventListener('keydown', event => {
            if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
            event.preventDefault();
            let nextIndex = index;
            if (event.key === 'ArrowDown') nextIndex = (index + 1) % fileButtons.length;
            if (event.key === 'ArrowUp') nextIndex = (index - 1 + fileButtons.length) % fileButtons.length;
            if (event.key === 'Home') nextIndex = 0;
            if (event.key === 'End') nextIndex = fileButtons.length - 1;
            const nextButton = fileButtons[nextIndex];
            selectFile(nextButton);
            nextButton.focus();
        });
    });
})();
