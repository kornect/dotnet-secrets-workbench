document.addEventListener("keydown", (event) => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        const input = document.getElementById("root-path");
        if (input) {
            event.preventDefault();
            input.focus({ preventScroll: true });
            input.select();
        }
    }
});
