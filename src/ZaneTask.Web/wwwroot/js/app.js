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
