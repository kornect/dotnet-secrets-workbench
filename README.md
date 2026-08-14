# Secret Workbench

A local-only Blazor GUI for viewing and bulk-editing ASP.NET Core development secrets across projects.

Secret Workbench runs on .NET 8, .NET 9, or .NET 10. It manages C#, F#, and Visual Basic projects (`.csproj`, `.fsproj`, `.vbproj`); the target framework of the selected project does not need to match the runtime used by Secret Workbench.

The JSON importer accepts ordinary nested `appsettings.json`-style objects. Objects are flattened with colon-delimited keys and array indexes become numeric segments, such as `Services:0:ApiKey`. Importing JSON replaces the editor's complete secret set; saving then removes keys omitted from the import.

## Install as a global tool

```bash
dotnet tool install --global SecretWorkbench
secret-workbench
```

Until the package is published to NuGet.org, install a locally packed build with:

```bash
dotnet pack SecretWorkbench/SecretWorkbench.csproj -c Release
dotnet tool install --global --add-source ./artifacts/packages SecretWorkbench
```

The command binds only to `127.0.0.1`, selects an available port, opens the default browser, and scans the current directory.

```text
--root <path>  Initial folder to scan
--port <port>  Loopback port; 0 selects an available port
--no-open      Print the URL without opening a browser
```

## Run from source

```bash
dotnet run --project SecretWorkbench
```

The app starts in the current working directory and finds `.csproj`, `.fsproj`, and `.vbproj` files. It reads and writes each project's `secrets.json` in the standard user-secrets store directly, so values round-trip exactly and a save replaces the whole set in one atomic write. The `dotnet user-secrets` CLI is still used to initialize a project, because that step edits the project file.

Secrets written here and by the CLI are interchangeable. Both resolve the file through the same `PathHelper` in `Microsoft.Extensions.Configuration.UserSecrets`, and both replace it by moving a sibling temporary file over the target, leaving the same owner-only file mode. So there is no path the CLI can write that Secret Workbench cannot, and an unwritable secrets folder fails for both — Secret Workbench just reports it in one sentence instead of a stack trace.

Previously opened projects appear in a recent-projects list, even when they are outside the folder currently being scanned. Secret Workbench stores only project paths and last-opened timestamps in `~/.secrets-workbench/secret-workbench.db`; secret values continue to live exclusively in .NET user-secrets storage. Missing projects are removed from the list automatically.

The project browser supports flat and collapsible folder-tree views. On desktop, drag its right divider to resize the sidebar (the width is remembered in the browser); double-click the divider to restore the default width.

## Safety model

- The server binds explicitly to the IPv4 loopback interface.
- Only requests addressed to `127.0.0.1` or `localhost` are served. Any other `Host` header is rejected with a 400, so a web page cannot reach the app by pointing a hostname it controls at loopback (DNS rebinding).
- No secrets are sent to a remote service.
- Secret values are masked in the editor by default.
- Values are not logged by the application.
- Saved `secrets.json` files are restricted to the current user on Linux and macOS.
- ASP.NET Core user secrets are development-only and are not encrypted at rest.

## Limitations

- Project discovery is capped at 250 projects per scan, breadth-first from the scan folder. The app says so when a scan is truncated.
- This version does not provide OS keychain storage or production secret-store integration.

## Test

```bash
dotnet test SecretWorkbench.slnx
```

## Publish a release

NuGet releases use trusted publishing, so the repository never stores a long-lived NuGet API key. Configure a NuGet.org trusted publishing policy for repository owner `kornect`, repository `dotnet-secrets-workbench`, workflow file `publishing.yml`, and GitHub environment `release`. Add the NuGet.org username (not email address) as the `NUGET_USER` secret in that environment.

Push a semantic-version tag to test, pack, smoke-test, and publish all supported tool frameworks:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The workflow can also be started manually with a package version from the GitHub Actions page. NuGet package versions are immutable, so every release must use a new version.
