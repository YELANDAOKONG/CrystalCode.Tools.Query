# Crystal Code Query

**A Crystal Code external tool set that queries JSON and XML files.**

This is a native dotnet tool set for [Crystal Code](https://github.com/YELANDAOKONG/CrystalCode). `query` selects one part of one JSON or XML file — a JSON Pointer into JSON, an element path into XML — and returns exactly that part, so the agent can read a large lock file, a minified manifest, or a project file without pulling the whole document into context. YAML is not supported yet.

[Features](#features) · [Tools](#tools) · [Selectors](#selectors) · [Paths and limits](#paths-and-limits) · [Install](#install) · [Build from source](#build-from-source)

## Features

- **One call, one exact part.** Only the selected node comes back, so a multi-megabyte document costs one small result instead of a full file read.
- **JSON Pointer.** RFC 6901 selectors such as `/scripts/build` and `/items/0` (0-based), with `~0` and `~1` escapes; the empty selector selects the whole document.
- **XML element paths without namespace fights.** `/Project/PropertyGroup/TargetFramework`, `//PackageReference`, `child[2]`, `@Version`, and `text()`; `[n]` picks the nth match (1-based). Matching compares local names, so `csproj`, `plist`, and `svg` documents with a default namespace need no prefixes.
- **Failures that teach.** A missing member, element, or attribute fails with the surrounding structure — available members, child elements, or attribute names — instead of a bare error.
- **Bounded and paged.** Files are capped at 16 MiB and binary files are refused; arrays and match lists page through `offset` and `limit`; oversized results and long scalars are truncated or replaced by a structure preview with explicit notes.
- **Workspace fence.** `path` is declared as a `pathArguments` entry in `tools.json`, so the host resolves and classifies it like `read`: workspace-bounded calls follow the source trust policy, outside paths require approval, and `.ssh`, `.gnupg`, and `.crystal/credentials.json` paths are refused outright.
- **Fits the manifest.** The set joins Plan and Work, declares `approval: always`, and loads one `ITool` from one assembly in one load context with a 60-second per-call timeout.

## Tools

| Tool | Catalog | What it does |
| :--- | :--- | :--- |
| `query` | Plan and Work | Return one selected part of one JSON or XML file |

### query arguments

| Argument | Default | Meaning |
| :--- | :--- | :--- |
| `path` | Required | Workspace-relative or absolute path to a JSON or XML file |
| `selector` | Whole document | JSON Pointer for JSON; element path for XML |
| `format` | Detected | `json` or `xml`, overriding extension and content detection |
| `offset` | 1 | 1-based first array item or match to return |
| `limit` | 50 (1–200) | Maximum array items or matches to return |

The format comes from the path extension (`.json`, `.jsonc`, `.xml`, `.csproj`, `.props`, `.targets`, `.vbproj`, `.fsproj`, `.plist`, `.svg`, `.xaml`, `.resx`, `.config`) or from the content; an explicit `format` wins over both. A YAML file is refused with a note to use `read`, `grep`, or `bash` instead.

## Selectors

### JSON Pointer

| Selector | Meaning |
| :--- | :--- |
| *(empty)* | The whole document |
| `/name` | The member `name` of the root object |
| `/scripts/build` | A nested member |
| `/items/0` | The first item of `items` (0-based) |
| `/a~1b` | The member `a/b` (`~1` is `/`, `~0` is `~`) |

A selected scalar returns its raw value text — `0.10` stays `0.10` and strings are not quoted. A selected object or array is printed as indented JSON. A missing member fails with the available members; array indexes reject leading zeros and `-`; `offset` and `limit` page arrays and are refused on non-arrays.

### XML paths

| Selector | Meaning |
| :--- | :--- |
| `/Project` | The document element `Project` |
| `/Project/PropertyGroup/TargetFramework` | A child path |
| `//PackageReference` | Every `PackageReference` at any depth |
| `/Project/ItemGroup[2]` | The second `ItemGroup` match (1-based) |
| `/Project/*` | Every child element |
| `/Project/@Sdk` | An attribute value |
| `/Project/PropertyGroup/TargetFramework/text()` | The trimmed text value |

Element and attribute names compare by local name, so a document with a default namespace or a prefix needs no special syntax. Multiple element matches print one serialized element per numbered block; `offset` and `limit` page the matches. A selector that finds nothing lists the available child elements or attribute names at the failing step.

## Paths and limits

Every resolved path must pass the host fence: workspace-relative or absolute paths resolve against the session workspace, outside paths require approval (or the reviewing model in Review and Audit), and `.ssh`, `.gnupg`, and `.crystal/credentials.json` paths are refused. Files larger than 16 MiB (`16777216` bytes) are refused before reading; a null byte in the first 8192 bytes of a file refuses it as binary. Results are capped at 50,000 characters, one scalar at 20,000, and one selector at 2,048. Arrays and match lists return at most 200 items per call (`limit` defaults to 50). The manifest gives the set 60 seconds per call.

## Install

Publish the set and place the published files, including `tools.json`, in one directory under `~/.crystal/tools`:

```bash
dotnet publish CrystalCode.Tools.Query/CrystalCode.Tools.Query.csproj -c Release -o ~/.crystal/tools/query
```

Restart the session, or run `/tools reload`, to load the set. A project set at `<workspace>/.crystal/tools/query/` replaces the home set for that workspace. The directory name is the set identity; this set installs as `query`.

## Build from source

You need the .NET 10 SDK and a sibling checkout of [Crystal](https://github.com/YELANDAOKONG/Crystal) at `../Crystal`. No CrystalCode checkout is needed at build time.

```bash
dotnet build CrystalCode.Tools.Query.sln
dotnet test CrystalCode.Tools.Query.sln
dotnet publish CrystalCode.Tools.Query/CrystalCode.Tools.Query.csproj
```

`Crystal.Tools` is referenced as a sibling source project. The production project adds no NuGet packages; the test project uses xUnit and Microsoft.NET.Test.Sdk only. `QueryTool` is the public `ITool` listed in `tools.json`; the host loads this class library in one isolated load context and wraps every call with its own approval, output truncation, and timeout. The JSON and XML selectors live in `Json` and `Xml`, the file pipeline in `Reading`, and result rendering in `Rendering`, all covered by the test project.
