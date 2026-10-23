export function setScrollLock(locked) {
    document.body.classList.toggle('dp-modal-open', locked);
}

export function focusFirst(container) {
    if (!container) {
        return;
    }

    const field = container.querySelector(
        'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled])'
    );
    const target = field
        ?? container.querySelector('button:not([disabled])')
        ?? container;

    target.focus();
}
