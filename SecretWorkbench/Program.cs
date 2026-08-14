using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using SecretWorkbench.Components;
using SecretWorkbench.Services;

SecretWorkbenchOptions toolOptions;
try
{
    toolOptions = SecretWorkbenchOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Run 'secret-workbench --help' for usage.");
    return 2;
}

if (toolOptions.ShowHelp)
{
    Console.WriteLine(SecretWorkbenchOptions.HelpText);
    return 0;
}

var packagedWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var contentRoot = Directory.Exists(packagedWebRoot)
    ? AppContext.BaseDirectory
    : Directory.GetCurrentDirectory();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [],
    ContentRootPath = contentRoot,
    WebRootPath = Path.Combine(contentRoot, "wwwroot")
});
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, toolOptions.Port));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<IUserSecretsService, DotNetUserSecretsService>();
builder.Services.AddSingleton<IRecentProjectsStore, SqliteRecentProjectsStore>();
builder.Services.AddSingleton(toolOptions);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.StartAsync();

var address = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?
    .Addresses.SingleOrDefault()
    ?? throw new InvalidOperationException("Secret Workbench started without a local address.");

Console.WriteLine($"Secret Workbench: {address}");
Console.WriteLine($"Project root: {toolOptions.RootPath}");

if (toolOptions.OpenBrowser)
{
    BrowserLauncher.TryOpen(address, Console.Error);
}

await app.WaitForShutdownAsync();
return 0;
