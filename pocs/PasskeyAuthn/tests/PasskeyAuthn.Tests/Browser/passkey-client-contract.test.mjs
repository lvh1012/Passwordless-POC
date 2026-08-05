import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const scriptPath = new URL("../../../src/PasskeyAuthn/wwwroot/js/passkey.js", import.meta.url);

/**
 * Loads the real browser script in the smallest DOM stub needed to inspect its exported contract.
 * This avoids copying serializer logic into the test and keeps the test dependency-free.
 * @returns {Record<string, Function>} The public Passkey client API.
 */
function loadPasskeyClient() {
    const window = {};
    const context = vm.createContext({
        ArrayBuffer,
        Uint8Array,
        atob: value => Buffer.from(value, "base64").toString("binary"),
        btoa: value => Buffer.from(value, "binary").toString("base64"),
        document: {
            addEventListener() {},
            getElementById() { return null; },
            querySelector() { return null; },
            querySelectorAll() { return []; },
        },
        navigator: { credentials: {} },
        PublicKeyCredential: class PublicKeyCredential {},
        window,
    });

    vm.runInContext(readFileSync(scriptPath, "utf8"), context, { filename: scriptPath.pathname });
    return window.PasskeyAuth;
}

/**
 * Creates a browser-like ArrayBuffer with bytes that exercise base64url alphabet and padding removal.
 * @param {...number} values Byte values to store.
 * @returns {ArrayBuffer} The isolated binary buffer.
 */
function bytes(...values) {
    return Uint8Array.from(values).buffer;
}

test("registration serialization matches the .NET WebAuthn JSON contract", () => {
    const client = loadPasskeyClient();
    const serialized = client.serializeCredential({
        id: "registration-id",
        rawId: bytes(251, 255),
        type: "public-key",
        authenticatorAttachment: "platform",
        getClientExtensionResults: () => ({ credProps: { rk: true } }),
        response: {
            clientDataJSON: bytes(1, 2, 3),
            attestationObject: bytes(4, 5, 6),
            getTransports: () => ["internal", "hybrid"],
        },
    });

    // JSON round-tripping removes cross-VM prototypes while preserving the actual wire contract.
    assert.deepEqual(JSON.parse(JSON.stringify(serialized)), {
        id: "registration-id",
        rawId: "-_8",
        type: "public-key",
        authenticatorAttachment: "platform",
        clientExtensionResults: { credProps: { rk: true } },
        response: {
            clientDataJSON: "AQID",
            attestationObject: "BAUG",
            transports: ["internal", "hybrid"],
        },
    });
});

test("assertion serialization includes required binary fields and omits a null user handle", () => {
    const client = loadPasskeyClient();
    const serialized = client.serializeCredential({
        id: "assertion-id",
        rawId: bytes(7, 8, 9),
        type: "public-key",
        authenticatorAttachment: null,
        getClientExtensionResults: () => ({ appid: false }),
        response: {
            clientDataJSON: bytes(10, 11, 12),
            authenticatorData: bytes(13, 14, 15),
            signature: bytes(16, 17, 18),
            userHandle: null,
        },
    });

    assert.deepEqual(JSON.parse(JSON.stringify(serialized)), {
        id: "assertion-id",
        rawId: "BwgJ",
        type: "public-key",
        clientExtensionResults: { appid: false },
        response: {
            clientDataJSON: "CgsM",
            authenticatorData: "DQ4P",
            signature: "EBES",
        },
    });
});
