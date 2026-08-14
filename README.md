# Secret Workbench

A local-only Blazor GUI for viewing and bulk-editing ASP.NET Core development secrets across projects.

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

The app starts in the current working directory, finds `.csproj` files, and uses the official `dotnet user-secrets` CLI for initialization, listing, removal, and JSON batch updates.

Previously opened projects appear in a recent-projects list, even when they are outside the folder currently being scanned. Secret Workbench stores only project paths and last-opened timestamps in `~/.secrets-workbench/secret-workbench.db`; secret values continue to live exclusively in .NET user-secrets storage. Missing projects are removed from the list automatically.

The project browser supports flat and collapsible folder-tree views. On desktop, drag its right divider to resize the sidebar (the width is remembered in the browser); double-click the divider to restore the default width.

## Safety model

- The server binds explicitly to the IPv4 loopback interface.
- No secrets are sent to a remote service.
- Secret values are masked in the editor by default.
- Values are not logged by the application.
- ASP.NET Core user secrets are development-only and are not encrypted at rest.

## MVP limitations

- The `dotnet user-secrets list` command emits text rather than JSON, so values containing literal newlines aren't represented reliably by the CLI output.
- Project discovery is capped at 250 projects per scan.
- This version does not provide OS keychain storage or production secret-store integration.

## Test

```bash
dotnet test SecretWorkbench.slnx
```
