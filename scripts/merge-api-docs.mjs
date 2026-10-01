// @ts-check
/**
 * Merges the DocFX API reference (docs/_site/) into the VitePress build (docs/.vitepress/dist/),
 * so the published site is one directory: VitePress pages at the root and in guide/, DocFX's
 * pages in api/ with its scripts and styles beside them.
 *
 * The two never write the same file. If they ever do, this stops rather than letting one
 * silently replace the other.
 *
 * Then it checks the links DocFX could not: the ones from the API pages into the VitePress
 * pages, which scripts/build-api-docs.mjs recorded in docs/_site/.vitepress-links.json.
 *
 * Run by `npm run docs:build`, after `vitepress build`.
 */
import { cpSync, existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const siteDir = join(repoRoot, 'docs', '_site');
const distDir = join(repoRoot, 'docs', '.vitepress', 'dist');
const linksFile = join(siteDir, '.vitepress-links.json');

for (const [dir, hint] of [[siteDir, 'npm run docs:api'], [distDir, 'vitepress build docs']]) {
  if (!existsSync(dir)) {
    console.error(`merge-api-docs: ${relative(repoRoot, dir)} is missing. Run \`${hint}\` first.`);
    process.exit(1);
  }
}

/** Every file under a directory, recursively, relative to it. */
function* walk(dir, root = dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) yield* walk(full, root);
    else yield relative(root, full);
  }
}

const docfxFiles = [...walk(siteDir)].filter(f => f !== '.vitepress-links.json');
const clashes = docfxFiles.filter(f => existsSync(join(distDir, f)));
if (clashes.length) {
  console.error('merge-api-docs: DocFX and VitePress both wrote these files:');
  for (const f of clashes) console.error('  ' + f);
  process.exit(1);
}

for (const f of docfxFiles) cpSync(join(siteDir, f), join(distDir, f));

const links = existsSync(linksFile) ? JSON.parse(readFileSync(linksFile, 'utf8')) : [];
const dead = links.filter(l => !existsSync(join(distDir, l)) || !statSync(join(distDir, l)).isFile());
if (dead.length) {
  console.error('merge-api-docs: the API pages link to VitePress pages that were not built:');
  for (const l of dead) console.error('  ' + l);
  process.exit(1);
}

console.log(`merge-api-docs: merged ${docfxFiles.length} DocFX files; ${links.length} link(s) into the VitePress pages resolve.`);
