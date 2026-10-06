let dotNetRef = null;
let mediaQuery = null;

export function isMobileViewport() {
    return window.matchMedia('(max-width: 991.98px)').matches;
}

export function setBodyScrollLock(locked) {
    if (!isMobileViewport()) {
        document.body.style.overflow = '';
        return;
    }

    document.body.style.overflow = locked ? 'hidden' : '';
}

export function registerViewportListener(ref) {
    dotNetRef = ref;
    mediaQuery = window.matchMedia('(max-width: 991.98px)');

    const onChange = (e) => {
        dotNetRef?.invokeMethodAsync('OnViewportChanged', e.matches);
    };

    mediaQuery.addEventListener('change', onChange);

    const onKeyDown = (e) => {
        if (e.key === 'Escape' && isMobileViewport()) {
            dotNetRef?.invokeMethodAsync('OnEscapeKey');
        }
    };

    document.addEventListener('keydown', onKeyDown);

    return {
        dispose: () => {
            mediaQuery?.removeEventListener('change', onChange);
            document.removeEventListener('keydown', onKeyDown);
            setBodyScrollLock(false);
            dotNetRef = null;
        }
    };
}
