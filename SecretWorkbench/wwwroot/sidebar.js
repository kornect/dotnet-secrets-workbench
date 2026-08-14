window.secretWorkbenchSidebar = {
    startResize(event) {
        if (window.matchMedia("(max-width: 59.99rem)").matches) return;
        event.preventDefault();
        const sidebar = event.currentTarget.closest("[data-project-sidebar]");
        const startX = event.clientX;
        const startWidth = sidebar.getBoundingClientRect().width;
        document.body.classList.add("is-resizing-sidebar");

        const move = moveEvent => {
            const width = Math.min(Math.max(startWidth + moveEvent.clientX - startX, 272), window.innerWidth * 0.65);
            sidebar.style.width = `${width}px`;
            localStorage.setItem("secret-workbench-sidebar-width", String(width));
        };
        const stop = () => {
            document.body.classList.remove("is-resizing-sidebar");
            window.removeEventListener("pointermove", move);
            window.removeEventListener("pointerup", stop);
        };
        window.addEventListener("pointermove", move);
        window.addEventListener("pointerup", stop, { once: true });
    },
    reset() {
        const sidebar = document.querySelector("[data-project-sidebar]");
        if (sidebar) sidebar.style.removeProperty("width");
        localStorage.removeItem("secret-workbench-sidebar-width");
    },
    restore() {
        const sidebar = document.querySelector("[data-project-sidebar]");
        const width = Number(localStorage.getItem("secret-workbench-sidebar-width"));
        if (sidebar && Number.isFinite(width) && width >= 272) sidebar.style.width = `${width}px`;
    }
};

window.secretWorkbenchSidebar.restore();
