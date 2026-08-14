using System.Text.Json;
using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class JsonSecretFlattenerTests
{
    [Fact]
    public void FlattenConvertsNestedObjectsToConfigurationKeys()
    {
        const string json = """
            {
              "ConnectionStrings": { "Main": "Server=localhost" },
              "Authentication": {
                "ClientId": "client-id",
                "ClientSecret": "secret"
              }
            }
            """;

        var result = JsonSecretFlattener.Flatten(json);

        Assert.Equal("Server=localhost", result["ConnectionStrings:Main"]);
        Assert.Equal("client-id", result["Authentication:ClientId"]);
        Assert.Equal("secret", result["Authentication:ClientSecret"]);
    }

    [Fact]
    public void FlattenConvertsArraysAndScalarTypes()
    {
        const string json = """
            {
              "Services": [
                { "Name": "primary", "Enabled": true, "Retries": 3 },
                { "Name": "backup", "ApiKey": null }
              ]
            }
            """;

        var result = JsonSecretFlattener.Flatten(json);

        Assert.Equal("primary", result["Services:0:Name"]);
        Assert.Equal("true", result["Services:0:Enabled"]);
        Assert.Equal("3", result["Services:0:Retries"]);
        Assert.Equal("backup", result["Services:1:Name"]);
        Assert.Equal(string.Empty, result["Services:1:ApiKey"]);
    }

    [Fact]
    public void FlattenRejectsANonObjectRoot()
    {
        var error = Assert.Throws<JsonException>(() => JsonSecretFlattener.Flatten("[\"secret\"]"));

        Assert.Contains("root must be an object", error.Message);
    }

    [Fact]
    public void FlattenRejectsPathsThatProduceTheSameConfigurationKey()
    {
        const string json = """
            {
              "Authentication:ClientId": "flat",
              "Authentication": { "ClientId": "nested" }
            }
            """;

        var error = Assert.Throws<JsonException>(() => JsonSecretFlattener.Flatten(json));

        Assert.Contains("duplicate configuration key", error.Message);
    }
}
