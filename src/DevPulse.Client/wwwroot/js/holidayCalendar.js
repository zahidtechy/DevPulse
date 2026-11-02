export function scrollToElement(elementId, block = 'nearest') {
    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    element.scrollIntoView({
        behavior: prefersReducedMotion ? 'auto' : 'smooth',
        block,
        inline: 'nearest'
    });
}
