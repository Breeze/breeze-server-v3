// @ts-check
/**
 * Builds the .NET API reference with DocFX, into docs/_site/, and fails on a warning.
 *
 * DocFX calls a dead link, an unresolved xref and a missing member a warning rather than an
 * error, and exits 0 - so without this a broken reference would build, and go live.
 *
 * Two kinds of warning are expected, and tolerated:
 *
 * - `docfx metadata`: "Found project reference without a matching metadata reference", once each
 *   for Breeze.Core and Breeze.Persistence. See DOCS.md.
 * - `docfx build`: "Invalid file link" to a page of the VitePress half of the site - the guide and
 *   the home page, linked from the API pages' top bar (docs/toc.yml) and their landing page.
 *   DocFX no longer builds those, so it cannot see them. They are written to
 *   docs/_site/.vitepress-links.json, and scripts/merge-api-docs.mjs checks that each one exists
 *   once the two halves are merged. So such a link is still checked, just later.
 *
 * Anything else stops the build. Run by `npm run docs:api`, and so by `docs:dev` and `docs:build`.
 */
import { spawnSync } from 'node:child_process';
import { rmSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const docfxJson = join(repoRoot, 'docs', 'docfx.json');
const siteDir = join(repoRoot, 'docs', '_site');

/** Runs `dotnet <args>`, echoing its output, and returns that output without colour codes. */
function dotnet(...args) {
  const result = spawnSync('dotnet', args, { cwd: repoRoot, encoding: 'utf8' });
  const output = (result.stdout ?? '') + (result.stderr ?? '');
  process.stdout.write(output);
  if (result.error) throw result.error;
  if (result.status !== 0) {
    console.error(`build-api-docs: dotnet ${args.join(' ')} failed (exit ${result.status}).`);
    process.exit(1);
  }
  return output.replace(/\x1b\[[0-9;]*m/g, '');
}

/** The warnings DocFX reported: `warning: ...` or `file(line,col): warning Code: ...`, not its summary. */
const warningsIn = output => output.split(/\r?\n/).filter(l => /(^|\)): ?warning\b|^\s*warning:/i.test(l.trim()));

function fail(step, unexpected) {
  console.error(`\nbuild-api-docs: docfx ${step} reported ${unexpected.length} unexpected warning(s):`);
  for (const w of unexpected) console.error('  ' + w.trim());
  console.error('\nThe docs are not built. Fix them, or see DOCS.md.');
  process.exit(1);
}

dotnet('tool', 'restore');

// A stale _site would hide a page that is no longer generated.
rmSync(siteDir, { recursive: true, force: true });

const metadataUnexpected = warningsIn(dotnet('docfx', 'metadata', docfxJson))
  .filter(w => !/Found project reference without a matching metadata reference: .*[\\/]src[\\/]Breeze\.(Core|Persistence)[\\/]/.test(w));
if (metadataUnexpected.length) fail('metadata', metadataUnexpected);

const buildWarnings = warningsIn(dotnet('docfx', 'build', docfxJson));
const vitepressLinks = new Set();
const buildUnexpected = buildWarnings.filter(w => {
  const m = /InvalidFileLink: Invalid file link:\(~\/([^)#?]+)[^)]*\)/.exec(w);
  if (!m) return true;
  vitepressLinks.add(m[1]);
  return false;
});
if (buildUnexpected.length) fail('build', buildUnexpected);

writeFileSync(join(siteDir, '.vitepress-links.json'), JSON.stringify([...vitepressLinks].sort(), null, 2) + '\n');
console.log(`build-api-docs: API reference built; ${vitepressLinks.size} link(s) into the VitePress pages to check after merging.`);
