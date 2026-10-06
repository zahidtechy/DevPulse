export function attachHoldGuards(element) {
    if (!element || element.dataset.dpHoldGuards === "1") {
        return {
            dispose() {}
        };
    }

    element.dataset.dpHoldGuards = "1";

    const onPointerDown = (event) => {
        try {
            element.setPointerCapture(event.pointerId);
        } catch {
            // Capture is best-effort on older WebViews.
        }

        event.preventDefault();
    };

    const onContextMenu = (event) => {
        event.preventDefault();
    };

    element.addEventListener("pointerdown", onPointerDown, { passive: false });
    element.addEventListener("contextmenu", onContextMenu);

    return {
        dispose() {
            element.removeEventListener("pointerdown", onPointerDown);
            element.removeEventListener("contextmenu", onContextMenu);
            delete element.dataset.dpHoldGuards;
        }
    };
}
