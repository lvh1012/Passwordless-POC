const form = document.querySelector('#magic-link-form');
const status = document.querySelector('#status');

form?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = form.querySelector('button');
    button.disabled = true;
    status.textContent = 'Sending…';

    try {
        const response = await fetch('/api/magic-links/request', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': form.dataset.antiforgery
            },
            body: JSON.stringify({ email: form.elements.email.value, returnUrl: '/dashboard' })
        });
        const body = await response.json();
        status.textContent = response.ok
            ? body.message
            : body.message ?? body.title ?? 'Unable to send a Magic Link.';
    } catch {
        status.textContent = 'Unable to reach the service. Try again.';
    } finally {
        button.disabled = false;
    }
});
