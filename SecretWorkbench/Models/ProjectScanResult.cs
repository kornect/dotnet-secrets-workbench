namespace SecretWorkbench.Models;

/// <summary>
/// The outcome of a workspace scan. <paramref name="WasTruncated"/> tells the UI that the scan hit
/// its limit, so it can say so instead of silently showing a partial index.
/// </summary>
public sealed record ProjectScanResult(IReadOnlyList<string> Projects, bool WasTruncated);
