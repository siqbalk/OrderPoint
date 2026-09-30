using System.Text.Json.Serialization;
using BuildingBlocks.Modules;
using BuildingBlocks.MultiTenancy;
using Catalog.Endpoints;
using Host.Setup;
using Identity.Endpoints;
using Inventory.Endpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Notifications.Endpoints;
using Reporting.Endpoints;
using Sales.Endpoints;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Composition root: every module is discovered here and only here.
    // Program.cs never references a module's Application/Domain/Infrastructure
    // directly — only its IModule entry point.
    IModule[] modules =
    [
        new IdentityModule(),
        new CatalogModule(),
        new InventoryModule(),
        new SalesModule(),
        new NotificationsModule(),
        new ReportingModule(),
    ];

    builder.Services.AddPlatformServices(modules);

    foreach (var module in modules)
    {
        module.AddModule(builder.Services, builder.Configuration);
    }

    builder.Services.AddPlatformSecurity(builder.Configuration, modules);
    builder.Services.AddPlatformRateLimiting(builder.Configuration);
    builder.Services.AddPlatformOutbox(builder.Configuration);
    builder.Services.AddPlatformHttp(builder.Configuration);

    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var app = builder.Build();

    // Request logging wraps the exception handler so it records the final status
    // code (e.g. 400 for a validation failure) rather than the raw exception.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("OrderPoint API"));
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseRateLimiter();
    app.UseAuthorization();

    // Liveness: the process is up. Readiness: every module's database is reachable.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

    foreach (var module in modules)
    {
        module.MapEndpoints(app);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Exposes the top-level-statement entry point so WebApplicationFactory&lt;Program&gt;
/// can host this app in-process for integration tests.
/// </summary>
public partial class Program;
