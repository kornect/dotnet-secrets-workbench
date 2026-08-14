namespace SecretWorkbench.Models;

public sealed class SecretEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsRevealed { get; set; }
}
