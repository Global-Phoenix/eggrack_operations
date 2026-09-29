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
                if (item === group) return;
                item.classList.remove('open');
                item.querySelector('[data-nav-group]')?.setAttribute('aria-expanded', 'false');
            });
            group.classList.toggle('open', open);
            button.setAttribute('aria-expanded', String(open));
        });
    });
})();