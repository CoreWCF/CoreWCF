# ASPDEPR008 Warning Suppression

## Summary
ASPDEPR008 warnings have been suppressed in test projects. These warnings indicate the use of deprecated `IWebHost` and `IWebHostBuilder` APIs.

## Why Suppressed?

### The Deprecated APIs
In .NET 10, Microsoft deprecated the following ASP.NET Core hosting APIs:
- `Microsoft.AspNetCore.Hosting.IWebHost`
- `Microsoft.AspNetCore.Hosting.IWebHostBuilder`  
- `WebHost.CreateDefaultBuilder()`
- `IHostBuilder.ConfigureWebHostDefaults()`
- `IWebHostBuilder.UseStartup<T>()`

### The Modern Replacement
The recommended modern pattern is:
```csharp
// Old deprecated pattern
var host = WebHost.CreateDefaultBuilder(args)
    .UseStartup<Startup>()
    .Build();  // Returns IWebHost (deprecated)

// New pattern
var builder = WebApplication.CreateBuilder(args);
// Configure services inline instead of Startup class
builder.Services.AddSingleton<IMyService, MyService>();
var app = builder.Build();  // Returns WebApplication which implements IHost
// Configure middleware inline
app.UseRouting();
app.MapGet("/", () => "Hello World!");
app.Run();
```

### Why Test Projects Still Use Old APIs
CoreWCF's test infrastructure heavily relies on `IWebHostBuilder` and the Startup class pattern for several reasons:

1. **Test Infrastructure Design**: The test helpers (ServiceHelper classes) are built around `IWebHostBuilder` pattern
2. **Startup Class Reuse**: Tests use Startup classes extensively to configure different scenarios  
3. **Scope of Change**: Migrating all tests to the new hosting model would require:
   - Rewriting all ServiceHelper.CreateWebHostBuilder methods
   - Converting hundreds of Startup classes to inline configuration
   - Updating every test that creates a host
   - Ensuring backwards compatibility with older .NET versions

4. **Test-Only Code**: These deprecated APIs are only used in **test code**, not in:
   - CoreWCF library itself (production code)
   - User-facing templates (already use modern pattern)
   - Documentation examples

## Current Solution
Added `<NoWarn>ASPDEPR008</NoWarn>` to test project files with explanatory comments:
```xml
<PropertyGroup>
  <!-- Suppress ASPDEPR008: IWebHost is obsolete. Test infrastructure uses IWebHostBuilder.
       Migration to WebApplication.CreateBuilder() pattern tracked separately. -->
  <NoWarn>$(NoWarn);ASPDEPR008</NoWarn>
</PropertyGroup>
```

## Future Work
A complete migration to the modern hosting model for tests should:

1. **Phase 1**: Create new test helpers using `WebApplication.CreateBuilder()`
2. **Phase 2**: Gradually migrate tests to use new helpers
3. **Phase 3**: Remove old helpers once all tests are migrated
4. **Consider**: Whether to maintain both patterns for backwards compatibility

This can be tracked in a separate issue/PR as it's a significant undertaking that affects the entire test suite.

## References
- [Microsoft's ASPDEPR008 documentation](https://aka.ms/aspnet/deprecate/008)
- [Migrating from ASP.NET Core 5.0 to 6.0](https://docs.microsoft.com/en-us/aspnet/core/migration/50-to-60)
- [Minimal hosting model](https://docs.microsoft.com/en-us/aspnet/core/migration/50-to-60#new-hosting-model)
