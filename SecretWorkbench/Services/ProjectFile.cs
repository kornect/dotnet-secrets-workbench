namespace SecretWorkbench.Services;

/// <summary>
/// The MSBuild project types Secret Workbench can manage. <c>dotnet user-secrets</c> supports each of them.
/// </summary>
public static class ProjectFile
{
    public static readonly string[] SupportedExtensions = [".csproj", ".fsproj", ".vbproj"];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string SupportedExtensionsForDisplay => string.Join(", ", SupportedExtensions);
}
