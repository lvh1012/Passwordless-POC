import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const directory = path.dirname(fileURLToPath(import.meta.url));
const sourceRoot = path.resolve(directory, '../../../src/MagicLinkAuthn/wwwroot/js');
const applicationRoot = path.resolve(directory, '../../../src/MagicLinkAuthn');
const callback = fs.readFileSync(path.join(sourceRoot, 'magic-link-callback.js'), 'utf8');
const request = fs.readFileSync(path.join(sourceRoot, 'magic-link.js'), 'utf8');
const indexPage = fs.readFileSync(path.join(applicationRoot, 'Pages/Index.cshtml'), 'utf8');
const onboardingPage = fs.readFileSync(path.join(applicationRoot, 'Pages/Onboarding.cshtml'), 'utf8');
const program = fs.readFileSync(path.join(applicationRoot, 'Program.cs'), 'utf8');

test('callback reads token from URL fragment and scrubs it before network use', () => {
    const readIndex = callback.indexOf('window.location.hash');
    const scrubIndex = callback.indexOf("history.replaceState(null, '', '/magic-link/callback')");
    const fetchIndex = callback.indexOf("fetch('/api/magic-links/prepare'");

    assert.ok(readIndex >= 0);
    assert.ok(scrubIndex > readIndex);
    assert.ok(fetchIndex > scrubIndex);
    assert.equal(callback.includes('window.location.search'), false);
});

test('state-changing requests carry antiforgery header and same-origin credentials', () => {
    for (const source of [request, callback]) {
        assert.match(source, /'X-CSRF-TOKEN'/);
        assert.match(source, /credentials:\s*'same-origin'/);
    }
});

test('form fallback cannot place an email address in the URL', () => {
    assert.match(indexPage, /<form[^>]+id="magic-link-form"[^>]+method="post"/);
});

test('Render proxy processing is limited to the nearest forwarded hop', () => {
    assert.match(program, /options\.ForwardLimit\s*=\s*1/);
});

test('onboarding requires full name while phone number remains optional', () => {
    assert.match(onboardingPage, /asp-for="Input\.FullName"[^>]+required/);
    const phoneInput = onboardingPage.match(/<input asp-for="Input\.PhoneNumber"[\s\S]*?\/>/)?.[0];
    assert.ok(phoneInput);
    assert.equal(/\srequired(?:\s|\/|>)/.test(phoneInput), false);
});
