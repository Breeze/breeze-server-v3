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
| `dotnet docfx serve docs/_site` | serves the last build, without rebuilding | http://localhost:8080/ |

The API reference is under `/api/` - for example
http://localhost:8080/api/Breeze.Persistence.EFCore.EFPersistenceManager-1.html and
http://localhost:8080/api/Breeze.Persistence.PersistenceManager.html.
To use another port, add `--port 8081`.

There is no hot reload. Every run of `dotnet docfx docs/docfx.json` rebuilds from the source you have checked out, so
after editing a doc comment in `src/` or a page in `docs/`, stop the server and run it again.

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
  toc.yml                   top navigation: Home, API reference
  api/                      GENERATED metadata (YAML) - do not edit, not committed
  _site/                    GENERATED site - not committed
```

- **The API reference** covers the five shipping packages: every `src/*/*.csproj`, so `tests/` and `tools/` are left
  out. The `metadata` section of `docfx.json` lists them; a new project under `src/` is picked up automatically.
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

Missing comments are not reported. CS1591 ("missing XML comment for publicly visible type or member") is suppressed
in `src/Directory.Build.props`, because a few hundred are missing; filling them in is its own task. To see the list,
build with the suppression lifted:

```bash
dotnet build src/Breeze.Persistence.NH/Breeze.Persistence.NH.csproj -f net10.0 -p:NoWarn=NU1900
```

(Overriding `NoWarn` on the command line replaces the value from `Directory.Build.props`.)

---

## Checks

`dotnet docfx docs/docfx.json` must end with `Build succeeded` and `0 error(s)`.

Warnings it currently reports, all known:

| warning | cause |
|---|---|
| `Found project reference without a matching metadata reference` (x2) | DocFX loads each project and its project references separately. Harmless: all five projects are documented and the links between them resolve. |
| `CS8632` in `NoAnonSerializationBinder.cs` | a `string?` annotation while `<Nullable>` is disabled. Goes away as nullable reference types are enabled. |
| `InvalidCref` in `NHibernateProxyJsonConverter` and `NHMetadataBuilder` | `<see cref="http://..."/>`; should be `href`. The URL still renders as a link. |

Any other warning is new. The compiler's doc-comment warnings (CS1570, CS1572, CS1573, CS1584, CS1587) show up in
`dotnet build` too.

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
- **Linking from the client docs.** The client site's *Server - .NET API reference* link still points at the 2.x
  site; it should point here once this is published.
- **Guide pages.** The site has only a home page and the API reference. Hand-written pages would go under `docs/`
  and in `docs/toc.yml`.
