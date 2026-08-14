using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Radzen;
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

if (!LoopbackPort.IsAvailable(toolOptions.Port))
{
    Console.Error.WriteLine($"Port {toolOptions.Port} is already in use.");
    Console.Error.WriteLine("Use '--port 0' to pick an available port, or '--port <port>' for a specific free one.");
    return 2;
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

// Only requests that address the app as loopback are served. Without this, a page the developer
// visits could point a hostname it controls at 127.0.0.1 (DNS rebinding) and read every secret.
builder.Services.Configure<HostFilteringOptions>(options =>
{
    options.AllowedHosts = ["127.0.0.1", "localhost"];
    options.AllowEmptyHosts = false;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();
builder.Services.AddSingleton<UserSecretsIdResolver>();
builder.Services.AddSingleton<IUserSecretsService>(provider =>
    new DotNetUserSecretsService(provider.GetRequiredService<UserSecretsIdResolver>()));
builder.Services.AddSingleton<IRecentProjectsStore, SqliteRecentProjectsStore>();
builder.Services.AddSingleton(toolOptions);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

try
{
    await app.StartAsync();
}
catch (IOException exception) when (exception.InnerException is AddressInUseException)
{
    Console.Error.WriteLine($"Port {toolOptions.Port} is already in use.");
    Console.Error.WriteLine("Use '--port 0' to pick an available port, or '--port <port>' for a specific free one.");
    return 2;
}

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
