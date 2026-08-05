import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import os from 'node:os';
import path from 'node:path';

const scriptPath = fileURLToPath(new URL('./discover-pocs.mjs', import.meta.url));
const repositoryRoot = path.resolve(path.dirname(scriptPath), '..');

/**
 * Creates the smallest valid POC fixture so each child-process test controls
 * only the manifest field or filesystem condition under test.
 */
async function writePoc(temporaryRoot, name, manifest, { includeCiScript = true } = {}) {
  const pocPath = path.join(temporaryRoot, 'pocs', name);
  await fs.mkdir(pocPath, { recursive: true });
  await fs.writeFile(path.join(pocPath, 'poc.json'), JSON.stringify(manifest));
  if (includeCiScript) {
    await fs.writeFile(path.join(pocPath, 'ci.sh'), '#!/usr/bin/env bash\n');
  }
  return pocPath;
}

/**
 * Runs the production discovery entry point as a real child process so tests
 * cover process cwd handling and the CLI error boundary together.
 */
function runDiscovery(workingDirectory) {
  return execFileSync(process.execPath, [scriptPath], {
    cwd: workingDirectory,
    encoding: 'utf8',
  });
}

/**
 * Asserts the CLI rejects a fixture and exposes a useful contract failure.
 */
function assertDiscoveryFails(workingDirectory, messagePattern) {
  assert.throws(
    () => runDiscovery(workingDirectory),
    (error) => {
      assert.equal(error.status, 1);
      assert.match(error.stderr.toString(), messagePattern);
      return true;
    },
  );
}

/**
 * Gives each filesystem test an isolated repository root and guarantees cleanup
 * even when the child process or an assertion fails.
 */
async function withTemporaryRoot(runTest) {
  const temporaryRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'poc-discovery-'));
  try {
    return await runTest(temporaryRoot);
  } finally {
    await fs.rm(temporaryRoot, { recursive: true, force: true });
  }
}

test('discovers the PasskeyAuthn POC through the real CLI process', () => {
  const output = runDiscovery(repositoryRoot);

  assert.deepEqual(JSON.parse(output), {
    include: [{
      id: 'passkey-authn',
      path: 'pocs/PasskeyAuthn',
      ciScript: 'ci.sh',
      deployHookSecret: 'RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN',
    }],
  });
});

test('emits an empty deploy hook secret when optional metadata is absent', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'optional-poc', { id: 'optional-poc' });
    await writePoc(temporaryRoot, 'null-secret-poc', { id: 'null-secret-poc', deployHookSecret: null });

    const output = runDiscovery(temporaryRoot);

    assert.deepEqual(JSON.parse(output), {
      include: [
        {
          id: 'null-secret-poc',
          path: 'pocs/null-secret-poc',
          ciScript: 'ci.sh',
          deployHookSecret: '',
        },
        {
          id: 'optional-poc',
          path: 'pocs/optional-poc',
          ciScript: 'ci.sh',
          deployHookSecret: '',
        },
      ],
    });
  });
});

test('ignores POCs nested below a direct child directory', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    const parentPath = await writePoc(temporaryRoot, 'parent-poc', { id: 'parent-poc' });
    await fs.mkdir(path.join(parentPath, 'nested-poc'));
    await fs.writeFile(path.join(parentPath, 'nested-poc', 'poc.json'), JSON.stringify({ id: 'nested-poc' }));
    await fs.writeFile(path.join(parentPath, 'nested-poc', 'ci.sh'), '#!/usr/bin/env bash\n');

    assert.deepEqual(JSON.parse(runDiscovery(temporaryRoot)), {
      include: [{ id: 'parent-poc', path: 'pocs/parent-poc', ciScript: 'ci.sh', deployHookSecret: '' }],
    });
  });
});

test('rejects a POC without poc.json', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    const pocPath = path.join(temporaryRoot, 'pocs', 'missing-manifest-poc');
    await fs.mkdir(pocPath, { recursive: true });
    await fs.writeFile(path.join(pocPath, 'ci.sh'), '#!/usr/bin/env bash\n');

    assertDiscoveryFails(temporaryRoot, /missing-manifest-poc\/poc\.json is missing or inaccessible/);
  });
});

test('rejects a POC without ci.sh', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'missing-ci-poc', { id: 'missing-ci-poc' }, { includeCiScript: false });

    assertDiscoveryFails(temporaryRoot, /missing-ci-poc\/ci\.sh is missing or inaccessible/);
  });
});

test('rejects an invalid manifest id', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'invalid-id-poc', { id: 'Invalid_ID' });

    assertDiscoveryFails(temporaryRoot, /invalid-id-poc\/poc\.json id must be lowercase kebab-case/);
  });
});

test('rejects an invalid deploy hook secret', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'invalid-hook-poc', {
      id: 'invalid-hook-poc',
      deployHookSecret: 'not-an-environment-name',
    });

    assertDiscoveryFails(temporaryRoot, /invalid-hook-poc\/poc\.json deployHookSecret must be an uppercase environment name/);
  });
});

test('rejects duplicate manifest ids across POCs', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'first-poc', { id: 'same-id' });
    await writePoc(temporaryRoot, 'second-poc', { id: 'same-id' });

    assertDiscoveryFails(temporaryRoot, /duplicate POC id: same-id/);
  });
});

test('rejects duplicate non-empty deploy hook secrets across POCs', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'first-poc', { id: 'first-poc', deployHookSecret: 'SHARED_HOOK' });
    await writePoc(temporaryRoot, 'second-poc', { id: 'second-poc', deployHookSecret: 'SHARED_HOOK' });

    assertDiscoveryFails(temporaryRoot, /duplicate deployHookSecret: SHARED_HOOK/);
  });
});

test('rejects a direct-child directory symlink that escapes pocs', async () => {
  await withTemporaryRoot(async (temporaryRoot) => {
    await writePoc(temporaryRoot, 'safe-poc', { id: 'safe-poc' });
    const externalDir = await fs.mkdtemp(path.join(os.tmpdir(), 'poc-discovery-outside-'));
    const linkPath = path.join(temporaryRoot, 'pocs', 'escaped-poc');
    try {
      await fs.writeFile(path.join(externalDir, 'poc.json'), JSON.stringify({ id: 'escaped-poc' }));
      await fs.writeFile(path.join(externalDir, 'ci.sh'), '#!/usr/bin/env bash\n');

      // Junctions avoid Windows Developer Mode/administrator requirements while still exercising directory reparse containment.
      try {
        await fs.symlink(externalDir, linkPath, process.platform === 'win32' ? 'junction' : 'dir');
      } catch (error) {
        assert.fail(`failed to create directory symlink on ${process.platform}: ${error.message}`);
      }

      assertDiscoveryFails(temporaryRoot, /escaped-poc path escapes its allowed root/);
    } finally {
      // Remove the link before its external target so Windows junction cleanup never traverses a missing target.
      await fs.rm(linkPath, { recursive: true, force: true });
      await fs.rm(externalDir, { recursive: true, force: true });
    }
  });
});
