// Small DOM helpers that Blazor cannot do on its own.
window.zanetask = {
    // Native <dialog> gives us a focus trap, Esc-to-close and focus return for free.
    showDialog(dialog, dotnetRef) {
        if (!dialog || dialog.open) return;
        dialog._dotnetRef = dotnetRef;
        if (!dialog._closeHooked) {
            dialog.addEventListener("close", () => dialog._dotnetRef?.invokeMethodAsync("OnNativeClose"));
            dialog._closeHooked = true;
        }
        dialog.showModal();
    },
    closeDialog(dialog) {
        if (dialog?.open) dialog.close();
    },
};

// Theme: "system" follows the OS; "light"/"dark" force one. Stored per browser; the resolved theme is set as
// <html data-theme="light|dark"> (CSS tokens switch on it) and reported to the desktop app for its title bar.
window.zanetask.theme = (() => {
    const key = "zanetask.theme";
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const read = () => {
        try { return localStorage.getItem(key) || "system"; } catch { return "system"; }
    };
    const apply = () => {
        const pref = read();
        const resolved = pref === "dark" || (pref !== "light" && media.matches) ? "dark" : "light";
        document.documentElement.setAttribute("data-theme", resolved);
        window.chrome?.webview?.postMessage({ theme: resolved });
    };
    const followSystem = () => { if (read() === "system") apply(); };
    media.addEventListener("change", followSystem);
    // Safety net: re-check when the window regains focus, in case a system change event was missed.
    window.addEventListener("focus", followSystem);
    apply();
    return {
        get: read,
        set(pref) {
            try { localStorage.setItem(key, pref); } catch { /* not remembered, but still applied below */ }
            if (pref !== "light" && pref !== "dark") {
                try { localStorage.removeItem(key); } catch { }
            }
            apply();
        },
    };
})();

// Firefox only starts an HTML5 drag when data is set on the transfer; Blazor cannot do that itself.
document.addEventListener("dragstart", (e) => {
    if (e.target instanceof Element && e.target.closest("[data-drag-task]")) {
        e.dataTransfer.setData("text/plain", "");
        e.dataTransfer.effectAllowed = "move";
    }
});

document.querySelector("#blazor-error-ui .dismiss")?.addEventListener("click", (e) => {
    e.currentTarget.parentElement.style.display = "none";
});
