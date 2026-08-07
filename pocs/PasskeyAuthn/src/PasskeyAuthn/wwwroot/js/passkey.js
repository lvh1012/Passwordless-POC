(() => {
    "use strict";

    class CeremonyError extends Error {
        /**
         * Creates an error category that maps to a safe browser message.
         * @param {string} name The stable error category.
         */
        constructor(name) {
            super(name);
            this.name = name;
        }
    }

    /**
     * Converts a base64url string into the ArrayBuffer shape required by WebAuthn fallback parsing.
     * @param {string} value A base64url value without required padding.
     * @returns {ArrayBuffer} The decoded binary value.
     */
    function base64urlToArrayBuffer(value) {
        const padding = "=".repeat((4 - (value.length % 4)) % 4);
        const binary = atob(value.replace(/-/g, "+").replace(/_/g, "/") + padding);
        return Uint8Array.from(binary, character => character.charCodeAt(0)).buffer;
    }

    /**
     * Converts an ArrayBuffer to unpadded base64url for the server's credential JSON contract.
     * @param {ArrayBuffer} value Browser credential binary data.
     * @returns {string} An unpadded base64url string.
     */
    function arrayBufferToBase64url(value) {
        const bytes = new Uint8Array(value);
        let binary = "";
        for (const byte of bytes) {
            binary += String.fromCharCode(byte);
        }

        return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    /**
     * Converts JSON credential descriptors for browsers without WebAuthn JSON parse helpers.
     * @param {PublicKeyCredentialDescriptor[] | undefined} descriptors JSON-safe descriptors.
     * @returns {PublicKeyCredentialDescriptor[] | undefined} WebAuthn-compatible descriptors.
     */
    function parseCredentialDescriptors(descriptors) {
        return descriptors?.map(descriptor => ({ ...descriptor, id: base64urlToArrayBuffer(descriptor.id) }));
    }

    /**
     * Adds the browser hint that lets WebAuthn offer browser-mediated cross-device authentication.
     * The browser selects the transport, so this client must not implement a QR or Bluetooth protocol itself.
     * @param {PublicKeyCredentialCreationOptionsJSON | PublicKeyCredentialRequestOptionsJSON} options Server-issued JSON options.
     * @returns {PublicKeyCredentialCreationOptionsJSON | PublicKeyCredentialRequestOptionsJSON} Options augmented for browser-mediated CDA.
     */
    function withHybridHint(options) {
        return { ...options, hints: ["hybrid"] };
    }

    /**
     * Parses registration options, using the platform helper when it is available.
     * @param {PublicKeyCredentialCreationOptionsJSON} options Server-issued JSON options.
     * @returns {PublicKeyCredentialCreationOptions} Browser-compatible creation options.
     */
    function parseCreationOptions(options) {
        if (typeof PublicKeyCredential.parseCreationOptionsFromJSON === "function") {
            return PublicKeyCredential.parseCreationOptionsFromJSON(options);
        }

        return {
            ...options,
            challenge: base64urlToArrayBuffer(options.challenge),
            user: { ...options.user, id: base64urlToArrayBuffer(options.user.id) },
            excludeCredentials: parseCredentialDescriptors(options.excludeCredentials),
        };
    }

    /**
     * Parses login options, using the platform helper when it is available.
     * @param {PublicKeyCredentialRequestOptionsJSON} options Server-issued JSON options.
     * @returns {PublicKeyCredentialRequestOptions} Browser-compatible request options.
     */
    function parseRequestOptions(options) {
        if (typeof PublicKeyCredential.parseRequestOptionsFromJSON === "function") {
            return PublicKeyCredential.parseRequestOptionsFromJSON(options);
        }

        return {
            ...options,
            challenge: base64urlToArrayBuffer(options.challenge),
            allowCredentials: parseCredentialDescriptors(options.allowCredentials),
        };
    }

    /**
     * Serializes browser credential fields manually because some password managers implement toJSON incompletely.
     * @param {PublicKeyCredential} credential The browser-produced WebAuthn credential.
     * @returns {object} The JSON-safe credential object expected inside CredentialRequest.Credential.
     */
    function serializeCredential(credential) {
        const response = credential.response;
        const serialized = {
            id: credential.id,
            rawId: arrayBufferToBase64url(credential.rawId),
            type: credential.type,
            // .NET Identity validates this root member even when no extension produced a value.
            clientExtensionResults: credential.getClientExtensionResults(),
            response: {
                clientDataJSON: arrayBufferToBase64url(response.clientDataJSON),
            },
        };

        if (credential.authenticatorAttachment != null) {
            serialized.authenticatorAttachment = credential.authenticatorAttachment;
        }

        if ("attestationObject" in response) {
            serialized.response.attestationObject = arrayBufferToBase64url(response.attestationObject);
            const transports = typeof response.getTransports === "function" ? response.getTransports() : null;
            if (transports != null) {
                serialized.response.transports = transports;
            }
        }

        if ("authenticatorData" in response) {
            serialized.response.authenticatorData = arrayBufferToBase64url(response.authenticatorData);
            serialized.response.signature = arrayBufferToBase64url(response.signature);
            if (response.userHandle != null) {
                serialized.response.userHandle = arrayBufferToBase64url(response.userHandle);
            }
        }

        return serialized;
    }

    /**
     * Maps WebAuthn and transport failures to messages that do not expose credential or server details.
     * @param {unknown} error The failure raised during the browser ceremony.
     * @returns {string} A safe message suitable for the user interface.
     */
    function mapError(error) {
        switch (error?.name) {
            case "NotAllowedError": return "The passkey request was cancelled or timed out.";
            case "AbortError": return "The passkey request was cancelled.";
            case "InvalidStateError": return "This passkey is already registered on this device.";
            case "NotSupportedError": return "This browser does not support passkeys.";
            case "ChallengeError": return "The passkey challenge is no longer valid. Please try again.";
            case "AuthenticationFailed": return "The passkey could not be verified. Please try again.";
            case "ValidationError": return "Enter a valid email address.";
            case "MalformedResponse": return "The server returned an invalid response. Please try again.";
            default: return "The request could not be completed. Please try again.";
        }
    }

    function csrfToken() {
        const token = document.querySelector('meta[name="csrf-token"]')?.content;
        if (!token) {
            throw new CeremonyError("ServerError");
        }

        return token;
    }

    async function postJson(url, body) {
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrfToken() },
            body: JSON.stringify(body),
        });
        if (!response.ok) {
            const isLoginCompletion = url === "/api/passkeys/login/complete";
            const isAuthenticationFailure =
                isLoginCompletion && (response.status === 400 || response.status === 401);
            const isRegistrationChallengeFailure =
                url === "/api/passkeys/register/complete" && response.status === 400;

            // Keep registration challenge UX stable while hiding all login verification details.
            const errorName = isAuthenticationFailure
                ? "AuthenticationFailed"
                : isRegistrationChallengeFailure
                    ? "ChallengeError"
                    : "ServerError";
            throw new CeremonyError(errorName);
        }

        return response;
    }

    async function readOptionsResponse(url, body) {
        const response = await postJson(url, body);
        try {
            return await response.json();
        } catch {
            throw new CeremonyError("MalformedResponse");
        }
    }

    async function readRegistrationOptions(email) {
        if (!email?.trim() || !/^\S+@\S+\.\S+$/.test(email)) {
            throw new CeremonyError("ValidationError");
        }

        return readOptionsResponse("/api/passkeys/register/options", { email });
    }

    async function readLoginOptions() {
        return readOptionsResponse("/api/passkeys/login/options", {});
    }

    function formFor(action) {
        return document.querySelector(`[data-passkey-form][data-passkey-action="${action}"]`);
    }

    function setMessage(form, selector, message) {
        const element = form?.querySelector(selector);
        if (element) {
            element.textContent = message;
            element.hidden = !message;
        }
    }

    async function runCeremony(action, email) {
        const form = formFor(action);
        const button = form?.querySelector("button");
        try {
            if (!window.PublicKeyCredential || !navigator.credentials) {
                throw new CeremonyError("NotSupportedError");
            }

            button?.setAttribute("disabled", "disabled");
            setMessage(form, "[data-passkey-error]", "");
            setMessage(form, "[data-passkey-status]", "Waiting for your passkey...");
            const isRegistration = action === "registration";
            const options = isRegistration
                ? await readRegistrationOptions(email)
                : await readLoginOptions();
            const credential = isRegistration
                ? await navigator.credentials.create({ publicKey: parseCreationOptions(withHybridHint(options)) })
                : await navigator.credentials.get({ publicKey: parseRequestOptions(withHybridHint(options)) });
            if (!credential) {
                throw new CeremonyError("NotAllowedError");
            }

            await postJson(
                isRegistration ? "/api/passkeys/register/complete" : "/api/passkeys/login/complete",
                { credential: serializeCredential(credential) });
            window.location.assign("/dashboard");
        } catch (error) {
            setMessage(form, "[data-passkey-status]", "");
            setMessage(form, "[data-passkey-error]", mapError(error));
        } finally {
            button?.removeAttribute("disabled");
        }
    }

    /**
     * Starts the username-less Passkey login ceremony.
     * @returns {Promise<void>} A promise that resolves after UI state has been updated.
     */
    async function startLogin() {
        await runCeremony("login");
    }

    /**
     * Starts the Passkey registration ceremony for the supplied email address.
     * @param {string} email The account email address.
     * @returns {Promise<void>} A promise that resolves after UI state has been updated.
     */
    async function startRegistration(email) {
        await runCeremony("registration", email);
    }

    async function logout() {
        const status = document.getElementById("logout-status");
        const error = document.getElementById("logout-error");
        const button = document.getElementById("logout-button");
        try {
            button?.setAttribute("disabled", "disabled");
            const response = await fetch("/api/auth/logout", {
                method: "POST",
                credentials: "same-origin",
                headers: { "X-CSRF-TOKEN": csrfToken() },
            });
            if (!response.ok) {
                throw new CeremonyError("ServerError");
            }

            window.location.assign("/");
        } catch (exception) {
            if (status) status.hidden = true;
            if (error) {
                error.textContent = mapError(exception);
                error.hidden = false;
            }
        } finally {
            button?.removeAttribute("disabled");
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll("[data-passkey-form]").forEach(form => {
            form.addEventListener("submit", event => {
                event.preventDefault();
                if (form.dataset.passkeyAction === "registration") {
                    void startRegistration(form.elements.email?.value);
                } else {
                    void startLogin();
                }
            });
        });
        document.getElementById("logout-button")?.addEventListener("click", () => void logout());
    });

    window.PasskeyAuth = { startLogin, startRegistration, serializeCredential, mapError };
})();
