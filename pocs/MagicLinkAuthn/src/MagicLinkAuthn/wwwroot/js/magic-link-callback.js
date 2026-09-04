const callback = document.querySelector('#magic-link-callback');
const status = document.querySelector('#status');

async function prepareMagicLink() {
    const parameters = new URLSearchParams(window.location.hash.slice(1));
    const token = parameters.get('token');
    history.replaceState(null, '', '/magic-link/callback');

    if (!token) {
        window.location.replace('/magic-link/invalid');
        return;
    }

    try {
        const response = await fetch('/api/magic-links/prepare', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': callback.dataset.antiforgery
            },
            body: JSON.stringify({ token })
        });
        if (!response.ok) {
            window.location.replace('/magic-link/invalid');
            return;
        }

        const body = await response.json();
        window.location.replace(body.redirectUrl);
    } catch {
        status.textContent = 'Unable to verify the link. Please request another one.';
    }
}

prepareMagicLink();
