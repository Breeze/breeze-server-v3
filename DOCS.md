# Building and viewing the docs

The .NET API reference for the Breeze server packages is a [DocFX](https://dotnet.github.io/docfx/) site,
generated from the XML doc comments in `src/`. The configuration lives in `docs/`.

Nothing is published yet. For now you view the site locally.

---

## TL;DR

```bash
dotnet tool restore                       # once - installs DocFX from .config/dotnet-tools.json
dotnet docfx docs/docfx.json --serve      # then open http://localhost:8080/
```

Stop the server with Ctrl+C.

---

## The commands

Run them from the repo root.

| command | what it does | where to look |
|---|---|---|
| `dotnet tool restore` | installs the DocFX version pinned in `.config/dotnet-tools.json` | - |
| `dotnet docfx docs/docfx.json` | generates the API metadata, then builds the static site | `docs/api/`, `docs/_site/` |
| `dotnet docfx docs/docfx.json --serve` | the same, then serves the site | http://localhost:8080/ |
| `dotnet docfx metadata docs/docfx.json` | regenerates only the API metadata (YAML) | `docs/api/` |
| `dotnet docfx build docs/docfx.json` | builds the site from the **existing** metadata, skipping Roslyn | `docs/_site/` |
| `dotnet docfx serve docs/_site` | serves the last build, without rebuilding | http://localhost:8080/ |

The API reference is under `/api/` - for example
http://localhost:8080/api/Breeze.Persistence.EFCore.EFPersistenceManager-1.html and
http://localhost:8080/api/Breeze.Persistence.PersistenceManager.html.
To use another port, add `--port 8081`.

There is no hot reload. Every run of `dotnet docfx docs/docfx.json` rebuilds from the source you have checked out, so
after editing a doc comment in `src/` or a page in `docs/`, stop the server and run it again.

**When you are editing prose, use `dotnet docfx build docs/docfx.json --serve` instead.** It reuses the API metadata
already in `docs/api/` rather than reading the projects again, which is the bulk of the time: about 5 seconds against
15 on this repo. Go back to the full command whenever you change a doc comment in `src/`, or the metadata is stale.

DocFX reads the projects through Roslyn and MSBuild; it does not need a prior `dotnet build`, but it does run a
NuGet restore of `src/` the first time.

Before you commit, run `dotnet docfx docs/docfx.json`: it is the check that the site still builds (see [Checks](#checks)).

---

## How it fits together

```
.config/dotnet-tools.json   pins the DocFX version (a local dotnet tool)
docs/
  docfx.json                what DocFX documents, and how
  index.md                  home page
  toc.yml                   top navigation: Home, Guide, API reference
  guide/                    hand-written pages
    toc.yml                 their order in the Guide tab
  snippets/                 the C# the guide shows - a real project, compiled
  api/                      GENERATED metadata (YAML) - do not edit, not committed
  _site/                    GENERATED site - not committed
```

- **The API reference** covers the five shipping packages: every `src/*/*.csproj`, so `tests/` and `tools/` are left
  out. The `metadata` section of `docfx.json` lists them; a new project under `src/` is picked up automatically.
- **The guide** is plain markdown under `docs/guide`, ordered by `docs/guide/toc.yml`. `docfx.json` picks up
  `**/*.{md,yml}`, so a new page needs no config change - only a line in that `toc.yml`. See
  [Writing guide pages](#writing-guide-pages).
- **The guide's C# is compiled.** It is not written in the markdown; it lives in
  `docs/snippets/Breeze.Docs.Snippets.csproj`, which project-references `Breeze.AspNetCore.NetCore` and
  `Breeze.Persistence.EFCore` and is in `Breeze.slnx`. The pages pull it in with
  `[!code-csharp[](../snippets/File.cs#Region)]`. Rename a public member and `dotnet build Breeze.slnx` fails on the
  documentation as well as on the tests, which is the point - see [Writing guide pages](#writing-guide-pages).
- **One target framework.** The packages multi-target `net8.0;net9.0;net10.0`. DocFX documents one of them, set by
  `"properties": { "TargetFramework": "net10.0" }` in `docfx.json`. The public API is the same on every target;
  only the Entity Framework Core version behind `Breeze.Persistence.EFCore` differs. Change the property if that ever
  stops being true.
- **Namespaces, not assemblies.** The table of contents is by namespace. They line up with the packages, except that
  `Breeze.AspNetCore.NetCore` puts its public types in the `Breeze.AspNetCore` namespace. Each type page names its
  assembly.
- **Links to .NET types** (`object`, `DbContext`, `JsonConverter` ...) go to learn.microsoft.com where DocFX can
  resolve them; links between Breeze packages are local.
- The site uses DocFX's `modern` template. `globalMetadata` in `docfx.json` sets the title and footer.
- Generated output - `docs/api/`, `docs/_site/` and `docs/obj/` - is gitignored.

---

## Writing XML doc comments

The API pages are only as good as the comments in `src/`. `src/Directory.Build.props` turns on
`GenerateDocumentationFile`, so the compiler checks them on every build.

DocFX renders these tags:

| tag | where it appears |
|---|---|
| `<summary>` | the description at the top of the page, and in member lists |
| `<param name="...">`, `<typeparam name="...">` | the Parameters and Type Parameters tables |
| `<returns>` | the Returns section |
| `<exception cref="...">` | the Exceptions table |
| `<remarks>` | a Remarks section below the summary |
| `<example>` | an Examples section; put code inside `<code>` |
| `<value>` | the property's value description |
| `<seealso cref="..."/>`, `<seealso href="..."/>` | a See Also list |
| `<see cref="..."/>`, `<see href="...">text</see>`, `<see langword="null"/>` | inline links and keywords |
| `<paramref name="..."/>`, `<typeparamref name="..."/>` | an inline parameter name |
| `<c>`, `<code>`, `<para>`, `<list>` | inline code, code blocks, paragraphs, lists |
| `<inheritdoc/>` | copies the comment from the base member or interface |

Link to other types with `<see cref="..."/>`, the same way the compiler resolves them:

```csharp
/// <summary>
/// Returns a <see cref="SaveResult"/>, like <see cref="PersistenceManager.SaveChanges(Newtonsoft.Json.Linq.JObject, TransactionSettings)"/>.
/// For EF Core, derive from <see cref="Breeze.Persistence.EFCore.EFPersistenceManager{T}"/>.
/// </summary>
```

Generic types use braces in a `cref` (`EFPersistenceManager{T}`), not angle brackets. A `cref` the compiler cannot
resolve is a compiler warning (CS1574) and does not become a working link.

Rules that have bitten before:

- **Escape `<` and `>` in comment text**, including in `<example>` and `<code>`: write `List&lt;Customer&gt;`.
  A bare `<Customer>` is read as an XML tag, the whole comment is badly formed, and **DocFX drops the comment for
  that member entirely** (the compiler reports CS1570).
- **URLs go in `href`, not `cref`.** `<see cref="http://..."/>` is a CS1584 warning and a DocFX `InvalidCref`
  warning. Use `<see href="http://...">text</see>`.
- **`<param>` names must match the real parameter names.** A stale one is CS1572; a missing one is CS1573.

**Missing comments are reported.** CS1591 ("missing XML comment for publicly visible type or member") is *not*
suppressed: every public member of every shipping package is documented, and the warning is what keeps it that way.
Add a public member and the build tells you to document it.

---

## Writing guide pages

The guide is plain markdown under `docs/guide`. To add a page, create the file and add it to `docs/guide/toc.yml`;
nothing else needs changing.

DocFX Flavored Markdown is CommonMark plus a few things worth using:

| syntax | what it does |
|---|---|
| `<xref:Breeze.Persistence.PersistenceManager>` | links to that type's API page, rendering its name |
| `[text](xref:Breeze.Persistence.PersistenceManager)` | the same, with your own link text |
| `` <xref:Breeze.Persistence.EFCore.EFPersistenceManager`1> `` | a generic type - backtick and arity, not `<T>` |
| `<xref:Breeze.Persistence.PersistenceManager.SaveChangesAsync*>` | a method, `*` for "whichever overload" |
| `> [!NOTE]`, `> [!TIP]`, `> [!IMPORTANT]`, `> [!WARNING]` | callout blocks |
| `[!code-csharp[](../../tests/Foo.cs#Region)]` | pulls a snippet out of real compiling source, by `#region` |
| `[!INCLUDE[](shared.md)]` | shares a fragment between pages |
| `# [EF Core](#tab/efcore)` | tabbed sections, for the EF Core / NHibernate fork |

Prefer an `xref` to a hand-written path: an `xref` that does not resolve is a build warning, while a wrong relative
link is only caught if it points at a missing *file*.

### The C# in the guide

**Do not write C# in a markdown fence.** It goes in `docs/snippets/`, inside a `#region`, and the page references
that region:

```markdown
[!code-csharp[](../snippets/SavingSnippets.cs#BeforeSaveEntity)]
```

`docs/snippets/Breeze.Docs.Snippets.csproj` is an ordinary project in `Breeze.slnx` that references the Breeze
projects in `src/`. Nothing runs it - it exists so the compiler checks the documentation. A renamed or removed public
member breaks the build here exactly as it would in an application, which is what stops the guide drifting from the
code it describes.

To add a snippet:

1. Put the code in the right file under `docs/snippets/`, wrapped in `#region Name` / `#endregion`.
2. Reference it from the page.
3. Run `dotnet build docs/snippets/Breeze.Docs.Snippets.csproj`.

Two things to watch:

- **Make each region a balanced unit** - a whole method, or a whole class including its closing brace. DocFX renders
  exactly what is between the markers, so a region that opens a class and ends before the `}` renders code that does
  not compile. Where the guide wants a whole controller, give the snippet file a small dedicated one rather than
  slicing a bigger class.
- **`using` directives are outside the regions**, at the top of the file, so they do not appear on the page. Where a
  reader needs them - the first controller, say - put them *inside* the namespace and inside the region, which is
  legal C# and renders as a reader expects. Otherwise name the namespace in the prose.

A fenced block is still right for what cannot compile here: JSON and HTTP examples, shell commands, TypeScript on the
client side, and C# for a dependency this project does not take - the PostgreSQL mapper in
[error-handling.md](guide/error-handling.md) would need Npgsql, so it stays inline.

> [!WARNING]
> Link checking here is weaker than on the client site. DocFX reports a broken file link or an unresolved `xref` as a
> **warning**, not an error, and it does not check `#anchor` fragments at all. `dotnet docfx docs/docfx.json` exits 0
> either way, so read the warning count - see [Checks](#checks).

---

## Checks

`dotnet docfx docs/docfx.json` must end with `Build succeeded` and `0 error(s)`.

It currently reports **exactly two warnings**, both known:

| warning | cause |
|---|---|
| `Found project reference without a matching metadata reference` (x2, for `Breeze.Core` and `Breeze.Persistence`) | DocFX loads each project and its project references separately. Harmless: all five projects are documented and the links between them resolve. |

Both come from the `metadata` step, so `dotnet docfx build docs/docfx.json` - which skips it - should report **0
warnings**. That makes the build-only command the sharper check while you are editing prose: any warning it prints is
yours, and is usually a broken link or an `xref` that did not resolve.

Any other warning is new. The compiler's doc-comment warnings (CS1570, CS1572, CS1573, CS1584, CS1587, CS1591) show
up in `dotnet build` too.

---

## Troubleshooting

**`dotnet docfx` - "Could not execute because the specified command or file was not found"**
The tool is not restored. Run `dotnet tool restore` from the repo root.

**A member's description is missing from its page**
Its XML comment is badly formed, usually an unescaped `<` in example code, and DocFX ignored it. DocFX prints
`InvalidXmlComment: ... Badly formed XML comment ignored for member ...`; the compiler prints CS1570. Escape the
brackets.

**My doc-comment change does not show up**
There is no hot reload. Stop the server and run `dotnet docfx docs/docfx.json --serve` again.

**Port 8080 is already in use**
Add `--port 8081` (or any free port).

---

## Not set up yet

- **Publishing.** The site is not deployed anywhere. `dotnet docfx docs/docfx.json` produces a static site in
  `docs/_site/` that any static host (GitHub Pages, for example) can serve; deploying it is still to be done.
  Until then the client site cannot link to a page here, only to this repo - see below.
- **Linking from the client docs.** The client site's *Server* menu points at
  [/server/dotnet](https://github.com/Breeze/breeze-client-v3/blob/master/docs/server/dotnet.md), a page there that
  says what this server gives a client and how to build this site locally. Point it straight at the published site
  once there is one, and trim that page's *Reading the server docs* section to a link.
- **More guide pages.** The guide covers getting started, the `PersistenceManager`, querying, saving, metadata and
  error handling. Not yet written: NHibernate specifics beyond what *Getting started* mentions, inheritance
  mapping, complex types, and a migration page for 7.x users (for now, `UPGRADE.md`).
- **NHibernate snippets.** `docs/snippets/` references `Breeze.Persistence.EFCore` only, so the guide's NHibernate
  mentions are prose rather than compiled code. Adding a reference to `Breeze.Persistence.NH` would let an NH page
  carry snippets on the same terms.
