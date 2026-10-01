import { defineConfig } from 'vitepress';
import { xrefPlugin } from './xref.mts';

// Where the site is served from. GitHub Pages serves this repo at /breeze-server-v3/, and
// scripts/publish-docs.ps1 sets DOCS_BASE to that; `docs:dev` and `docs:build` serve from /.
const base = process.env.DOCS_BASE ?? '/';

// The .NET API reference is built by DocFX, not VitePress, and merged into the built site at
// api/ (scripts/merge-api-docs.mjs). target: '_self' makes a link to it a plain page load
// rather than a VitePress route, which would 404.
const api = { link: '/api/', target: '_self' } as const;

export default defineConfig({
  title: 'Breeze Server',
  description: 'Breeze server-side packages for .NET',
  lang: 'en-US',
  base,
  cleanUrls: true,
  lastUpdated: true,
  ignoreDeadLinks: false,

  // DocFX also owns docs/api/ (its generated YAML) and docs/_site/ (its output); snippets/ is a
  // C# project the guide includes code from.
  srcExclude: ['api/**', 'api-landing/**', '_site/**', 'obj/**', 'snippets/**', 'README.md'],

  markdown: {
    config: md => xrefPlugin(md, base),
  },

  themeConfig: {
    search: { provider: 'local' },

    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'API', ...api },
      {
        text: 'Upgrading from 7.x',
        items: [
          { text: 'Upgrading from 7.x', link: 'https://github.com/Breeze/breeze-server-v3/blob/master/UPGRADE.md' },
          // The old site redirects its pages here; ?v2 is what keeps a reader there instead.
          { text: '7.x docs', link: 'https://breeze.github.io/doc-net/?v2' },
        ],
      },
      {
        // The mirror of the client docs' "Server" menu. Nothing checks these at either end.
        text: 'Client',
        items: [
          { text: 'Breeze client docs', link: 'https://breeze.github.io/breeze-client-v3/' },
          { text: 'Using a Breeze .NET server', link: 'https://breeze.github.io/breeze-client-v3/server/dotnet' },
        ],
      },
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Guide',
          items: [
            { text: 'Getting started', link: '/guide/getting-started' },
            { text: 'The PersistenceManager', link: '/guide/persistence-manager' },
            { text: 'Querying', link: '/guide/querying' },
            { text: 'Saving', link: '/guide/saving' },
            { text: 'Security', link: '/guide/security' },
            { text: 'Metadata', link: '/guide/metadata' },
            { text: 'Error handling', link: '/guide/error-handling' },
          ],
        },
        {
          text: 'Reference',
          items: [{ text: '.NET API reference', ...api }],
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Breeze/breeze-server-v3' },
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © IdeaBlade',
    },

    editLink: {
      pattern: 'https://github.com/Breeze/breeze-server-v3/edit/master/docs/:path',
      text: 'Edit this page on GitHub',
    },
  },
});
