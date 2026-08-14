using System.Globalization;
using System.Text.Json;

namespace SecretWorkbench.Services;

public static class JsonSecretFlattener
{
    public static IReadOnlyDictionary<string, string> Flatten(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The JSON root must be an object, like appsettings.json.");
        }

        var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenObject(document.RootElement, null, flattened);
        return flattened;
    }

    private static void FlattenObject(JsonElement element, string? parentPath, Dictionary<string, string> destination)
    {
        foreach (var property in element.EnumerateObject())
        {
            var path = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}:{property.Name}";
            FlattenValue(property.Value, path, destination);
        }
    }

    private static void FlattenArray(JsonElement element, string parentPath, Dictionary<string, string> destination)
    {
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            FlattenValue(item, $"{parentPath}:{index.ToString(CultureInfo.InvariantCulture)}", destination);
            index++;
        }
    }

    private static void FlattenValue(JsonElement element, string path, Dictionary<string, string> destination)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                FlattenObject(element, path, destination);
                break;
            case JsonValueKind.Array:
                FlattenArray(element, path, destination);
                break;
            case JsonValueKind.String:
                Add(destination, path, element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Number:
                Add(destination, path, element.GetRawText());
                break;
            case JsonValueKind.True:
                Add(destination, path, bool.TrueString.ToLowerInvariant());
                break;
            case JsonValueKind.False:
                Add(destination, path, bool.FalseString.ToLowerInvariant());
                break;
            case JsonValueKind.Null:
                Add(destination, path, string.Empty);
                break;
            default:
                throw new JsonException($"Unsupported JSON value at '{path}'.");
        }
    }

    private static void Add(Dictionary<string, string> destination, string path, string value)
    {
        if (!destination.TryAdd(path, value))
        {
            throw new JsonException($"The JSON produces the duplicate configuration key '{path}'.");
        }
    }
}
