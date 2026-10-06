const listeners = new WeakMap();

export function registerOutsideClick(element, dotNetRef) {
    unregisterOutsideClick(element);

    const handler = (event) => {
        if (!element || element.contains(event.target)) {
            return;
        }

        dotNetRef.invokeMethodAsync("CloseFromOutside");
    };

    const timer = setTimeout(() => {
        document.addEventListener("pointerdown", handler, true);
    }, 0);

    listeners.set(element, { handler, timer });
}

export function unregisterOutsideClick(element) {
    const entry = listeners.get(element);
    if (!entry) {
        return;
    }

    clearTimeout(entry.timer);
    document.removeEventListener("pointerdown", entry.handler, true);
    listeners.delete(element);
}
