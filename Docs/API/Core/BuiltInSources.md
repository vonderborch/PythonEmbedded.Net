# Built-in Sources

Namespace: `PythonEmbedded.Net.Sources`. Both types are `internal sealed`, implementing `IPythonSource` (via `PythonSourceBase`; see [Extensibility.md](Extensibility.md)). Together they form the default `PythonOptions.Sources` list, tried in this order: `DirectorySource("bundled", ...)` then `AstralSource()`.

## `DirectorySource.cs`

`internal sealed class DirectorySource : PythonSourceBase` — installs Python from python-build-standalone `install_only` archives found in a local directory. Backs both the bundled runtime-package convention (`python-embedded-runtimes/` next to the app, wired up as the default `"bundled"` source in `PythonOptions`'s constructor) and user-supplied archive directories added via `PythonOptions.AddDirectorySource`.

- Constructor: `DirectorySource(string name, string directory)` — `name` becomes `Name`; `directory` is the folder scanned for archives.
- `Name` (`override string`) — whatever was passed to the constructor (e.g. `"bundled"`, `"directory"`, or a custom name).
- `TryInstallAsync(PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct)` — returns `null` immediately if the source directory doesn't exist. Otherwise lists file names in the directory and calls `ArchiveName.SelectBest(names, request, context.Platform)` (see [Internals.md](Internals.md)); returns `null` if nothing matches. On a match, extracts the archive directly via `ArchiveExtractor.ExtractAsync` (no download step — it's already local) and returns a `PythonInstallInfo` with no `Checksum` (local archives aren't checksum-verified) and no `RelativePythonPath` (left for `PythonHost` to probe).

## `AstralSource.cs`

`internal sealed class AstralSource : PythonSourceBase` — downloads Python from `astral-sh/python-build-standalone` GitHub releases. The latest release tag is resolved via the GitHub API (ETag-cached through `SourceContext.GetCachedJsonAsync`); the tag's `SHA256SUMS` asset supplies the full file list and checksums, so the paginated/rate-limited GitHub *assets* API is never needed and download URLs are fully predictable (`https://github.com/{repo}/releases/download/{tag}/{fileName}`).

- `Name` → `"astral"` (constant, not settable).
- `TryInstallAsync(...)`:
  1. `ResolveLatestTagAsync` — fetches `GET /repos/astral-sh/python-build-standalone/releases/latest` through the cache (key `"astral-latest-release"`, TTL = `context.ReleaseCacheTtl`), deserializing just the `tag_name` field into a private `ReleaseDto`. If offline with nothing cached, `SourceContext` throws `PythonException(Offline)`, which this method catches specifically and turns into a `null` return (logged at debug level) — letting other sources speak, or a clear `VersionNotFound` bubble up instead of a confusing offline error from this one source.
  2. `GetChecksumsAsync(tag, ...)` — downloads and parses `SHA256SUMS` for that tag (cached forever under `<cache>/astral/SHA256SUMS-{tag}`, since a release's checksums are immutable once published; throws `PythonException(Offline)` if offline and not yet cached, or `PythonException(DownloadFailed)` on a failed fetch). Parses lines of the form `<sha256hex>  <filename>` into a `Dictionary<string,string>` (filename → checksum), keyed by finding the space at index 64 (fixed-width hex length) to split hash from name.
  3. `ArchiveName.SelectBest(checksums.Keys, request, context.Platform)` — returns `null` (logged at debug level) if nothing in this release satisfies the request/platform.
  4. Downloads the selected archive via `context.DownloadAsync(uri, checksums[archive.FileName], ct)` (checksum-verified), extracts it, and returns a `PythonInstallInfo` carrying the verified checksum.
- Private nested `ReleaseDto(string TagName)` — the only field consumed from the GitHub releases API response, mapped from JSON's `tag_name` via `[JsonPropertyName]`.

## See also

- [Extensibility.md](Extensibility.md) — the `IPythonSource` contract and `SourceContext` plumbing both sources rely on.
- [Internals.md](Internals.md) — `ArchiveName`/`ArchiveExtractor`, and `PythonHost`'s source-iteration loop.
- [../../Troubleshooting.md](../../Troubleshooting.md#version-resolution-issues) — GitHub rate limiting and offline-mode guidance from a user's perspective.
