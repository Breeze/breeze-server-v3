import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import yaml from 'js-yaml';
import type MarkdownIt from 'markdown-it';

/**
 * Resolves DocFX cross-references in the guide: `<xref:Breeze.Persistence.PersistenceManager>`
 * and `[text](xref:Breeze.Core)`, the same syntax DocFX itself understands.
 *
 * The API reference is still built by DocFX, which writes every uid it generated to
 * `_site/xrefmap.yml` with the page and anchor it ended up on. Each xref link is looked up
 * there, so a link lands wherever DocFX actually put the member - no guessing at its file
 * naming. An `<xref:...>` with no text shows the member's name, as DocFX would, in code font,
 * as the client docs show API names.
 *
 * The API pages are not VitePress pages, so the links carry target="_self": the VitePress
 * router leaves those alone, and the browser loads the DocFX page. VitePress also skips its own
 * href handling for them, so the site base is added here.
 *
 * An unknown uid fails the build, as an unresolved xref did when DocFX built the guide too.
 */

interface XrefEntry { uid: string; name: string; href: string }

const xrefmapPath = fileURLToPath(new URL('../_site/xrefmap.yml', import.meta.url));

function loadXrefmap(): Map<string, XrefEntry> {
  if (!existsSync(xrefmapPath)) {
    throw new Error(`${xrefmapPath} is missing. Run \`npm run docs:api\` first - the guide's API ` +
                    'links are resolved against what DocFX generated.');
  }
  const doc = yaml.load(readFileSync(xrefmapPath, 'utf8')) as { references: XrefEntry[] };
  return new Map(doc.references.map(r => [r.uid, r]));
}

export function xrefPlugin(md: MarkdownIt, base: string) {
  const xrefs = loadXrefmap();

  md.core.ruler.push('docfx_xref', state => {
    for (const block of state.tokens) {
      if (block.type !== 'inline' || !block.children) continue;
      const children = block.children;

      for (let i = 0; i < children.length; i++) {
        const open = children[i];
        if (open.type !== 'link_open') continue;
        const href = open.attrGet('href') ?? '';
        if (!href.startsWith('xref:')) continue;

        const uid = decodeURIComponent(href.slice('xref:'.length));
        const entry = xrefs.get(uid);
        if (!entry) {
          const where = (state.env as { relativePath?: string }).relativePath ?? 'a page';
          throw new Error(`${where}: no API member with uid '${uid}' (see docs/_site/xrefmap.yml).`);
        }

        open.attrSet('href', `${base}${entry.href}`.replace(/\/+/g, '/'));
        open.attrSet('target', '_self');

        // <xref:...> is parsed as an autolink, whose text is the uid. Show the name instead.
        if (open.markup === 'autolink') {
          const text = children[i + 1];
          text.type = 'code_inline';
          text.content = entry.name;
        }
      }
    }
  });
}
