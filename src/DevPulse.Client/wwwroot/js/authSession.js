const refreshLockName = "devpulse-auth-refresh";
const refreshLeaseKey = "devpulse.auth.refresh-lock.v1";
const storageListeners = new Map();

export function getSession(key) {
    return localStorage.getItem(key);
}

export function setSession(key, json) {
    localStorage.setItem(key, json);
}

export function removeSession(key) {
    localStorage.removeItem(key);
}

export function registerStorageListener(key, dotNetReference) {
    const id = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
    const listener = event => {
        if (event.storageArea === localStorage && event.key === key) {
            dotNetReference.invokeMethodAsync("OnStorageChanged", event.newValue);
        }
    };

    window.addEventListener("storage", listener);
    storageListeners.set(id, listener);
    return id;
}

export function unregisterStorageListener(id) {
    const listener = storageListeners.get(id);
    if (listener) {
        window.removeEventListener("storage", listener);
        storageListeners.delete(id);
    }
}

export async function runWithRefreshLock(dotNetReference, rejectedAccessToken) {
    if (navigator.locks) {
        return navigator.locks.request(
            refreshLockName,
            () => dotNetReference.invokeMethodAsync("RefreshUnderLockAsync", rejectedAccessToken));
    }

    return runWithStorageLease(dotNetReference, rejectedAccessToken);
}

async function runWithStorageLease(dotNetReference, rejectedAccessToken) {
    const owner = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;

    for (let attempt = 0; attempt < 100; attempt++) {
        const now = Date.now();
        const current = readLease();

        if (!current || current.expiresAt <= now) {
            localStorage.setItem(refreshLeaseKey, JSON.stringify({ owner, expiresAt: now + 30000 }));
            const acquired = readLease();

            if (acquired?.owner === owner) {
                try {
                    return await dotNetReference.invokeMethodAsync(
                        "RefreshUnderLockAsync",
                        rejectedAccessToken);
                } finally {
                    if (readLease()?.owner === owner) {
                        localStorage.removeItem(refreshLeaseKey);
                    }
                }
            }
        }

        await new Promise(resolve => setTimeout(resolve, 50 + Math.floor(Math.random() * 50)));
    }

    return false;
}

function readLease() {
    try {
        const value = localStorage.getItem(refreshLeaseKey);
        return value ? JSON.parse(value) : null;
    } catch {
        return null;
    }
}
