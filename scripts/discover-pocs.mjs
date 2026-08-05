import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = path.resolve(process.cwd());
const pocsRoot = path.join(repositoryRoot, 'pocs');
const idPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const envNamePattern = /^[A-Z][A-Z0-9_]*$/;

function fail(message) {
  throw new Error(`POC discovery failed: ${message}`);
}

/**
 * Returns true only when targetPath is a strict descendant of rootPath.
 * Keeping the relative-path checks together prevents traversal and root-equality edge cases from being missed.
 */
function isStrictlyInside(rootPath, targetPath) {
  const relativePath = path.relative(rootPath, targetPath);
  return relativePath !== ''
    && relativePath !== '..'
    && !relativePath.startsWith(`..${path.sep}`)
    && !path.isAbsolute(relativePath);
}

function assertStrictlyInside(rootPath, targetPath, description) {
  if (!isStrictlyInside(rootPath, targetPath)) {
    fail(`${description} escapes its allowed root`);
  }
}

async function resolveContainedFile(pocName, canonicalPocPath, fileName) {
  let canonicalFilePath;
  try {
    canonicalFilePath = await fs.realpath(path.join(canonicalPocPath, fileName));
  } catch (error) {
    fail(`${pocName}/${fileName} is missing or inaccessible (${error.message})`);
  }

  assertStrictlyInside(canonicalPocPath, canonicalFilePath, `${pocName}/${fileName}`);
  return canonicalFilePath;
}

/**
 * Validates a manifest and returns only the fixed CI contract consumed by Actions.
 * The script name is deliberately not read from metadata: every POC owns `ci.sh`.
 */
async function readPoc(pocEntry, canonicalPocsRoot) {
  const pocPath = path.join(pocsRoot, pocEntry.name);
  let canonicalPocPath;
  try {
    canonicalPocPath = await fs.realpath(pocPath);
  } catch (error) {
    fail(`${pocEntry.name} POC directory is missing or inaccessible (${error.message})`);
  }
  assertStrictlyInside(canonicalPocsRoot, canonicalPocPath, `${pocEntry.name} path`);

  const manifestPath = await resolveContainedFile(pocEntry.name, canonicalPocPath, 'poc.json');
  let manifest;
  try {
    manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
  } catch (error) {
    fail(`${pocEntry.name}/poc.json is missing or invalid JSON (${error.message})`);
  }

  if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)) {
    fail(`${pocEntry.name}/poc.json must contain an object`);
  }
  if (typeof manifest.id !== 'string' || !idPattern.test(manifest.id)) {
    fail(`${pocEntry.name}/poc.json id must be lowercase kebab-case`);
  }
  // Deploy hooks are optional for POCs that do not have an external deployment target.
  const deployHookSecret = manifest.deployHookSecret ?? '';
  if (typeof deployHookSecret !== 'string' || (deployHookSecret !== '' && !envNamePattern.test(deployHookSecret))) {
    fail(`${pocEntry.name}/poc.json deployHookSecret must be an uppercase environment name`);
  }

  const ciScriptPath = await resolveContainedFile(pocEntry.name, canonicalPocPath, 'ci.sh');
  try {
    await fs.access(ciScriptPath);
  } catch {
    fail(`${pocEntry.name}/ci.sh is required`);
  }

  return {
    id: manifest.id,
    path: path.relative(repositoryRoot, canonicalPocPath).split(path.sep).join('/'),
    ciScript: 'ci.sh',
    deployHookSecret,
  };
}

/**
 * Discovers isolated POCs from direct children only, preventing nested monorepo
 * projects from being silently included in the deployment matrix.
 */
async function discover() {
  let entries;
  try {
    entries = await fs.readdir(pocsRoot, { withFileTypes: true });
  } catch (error) {
    fail(`cannot read pocs root (${error.message})`);
  }

  const canonicalPocsRoot = await fs.realpath(pocsRoot);
  const pocEntries = entries.filter((entry) => entry.isDirectory() || entry.isSymbolicLink());
  if (pocEntries.length === 0) {
    fail('no POC directories found');
  }

  const include = await Promise.all(pocEntries.map((entry) => readPoc(entry, canonicalPocsRoot)));
  const ids = new Set();
  const deployHookSecrets = new Set();
  for (const poc of include) {
    if (ids.has(poc.id)) {
      fail(`duplicate POC id: ${poc.id}`);
    }
    ids.add(poc.id);

    if (poc.deployHookSecret !== '') {
      if (deployHookSecrets.has(poc.deployHookSecret)) {
        fail(`duplicate deployHookSecret: ${poc.deployHookSecret}`);
      }
      deployHookSecrets.add(poc.deployHookSecret);
    }
  }

  include.sort((left, right) => left.id < right.id ? -1 : left.id > right.id ? 1 : 0);
  return { include };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  discover()
    .then((matrix) => process.stdout.write(`${JSON.stringify(matrix)}\n`))
    .catch((error) => {
      process.stderr.write(`${error.message}\n`);
      process.exitCode = 1;
    });
}
