const scrollLocks = new Set();

export function setScrollLock(owner, locked) {
    if (locked) {
        scrollLocks.add(owner);
    } else {
        scrollLocks.delete(owner);
    }

    document.body.classList.toggle('dp-modal-open', scrollLocks.size > 0);
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
