using System.Text;
using System.Threading.RateLimiting;
using BuildingBlocks.Modules;
using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using BuildingBlocks.Security;
using BuildingBlocks.Web;
using Host.ErrorHandling;
using Host.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Host.Setup;

/// <summary>
/// Cross-cutting platform wiring that belongs to the composition root rather
/// than to any module: tenancy, authentication, authorization, rate limiting,
/// the outbox worker, and HTTP concerns. None of it knows about a specific module.
/// </summary>
internal static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services, IReadOnlyCollection<IModule> modules)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddSingleton<IPermissionCatalog>(new PermissionCatalog(modules.SelectMany(m => m.Permissions)));

        return services;
    }

    public static IServiceCollection AddPlatformSecurity(this IServiceCollection services, IConfiguration configuration, IReadOnlyCollection<IModule> modules)
    {
        // Validated at startup so a missing or short signing key fails fast
        // instead of surfacing as 401s on every request.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwt) =>
            {
                // Keep raw JWT claim names ("sub", "tenant_id", "permission") on the principal.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var module in modules)
            {
                module.AddAuthorizationPolicies(options);
            }
        });

        return services;
    }

    public static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        var authenticationPermitLimit = section.GetValue("AuthenticationPermitsPerMinute", 10);
        var tenantPermitLimit = section.GetValue("TenantPermitsPerMinute", 600);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Brute-force protection for anonymous credential endpoints, per client IP.
            options.AddPolicy(RateLimitPolicies.Authentication, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authenticationPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // Fair use across tenants: one noisy tenant cannot starve the others.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.User.FindFirst(ClaimNames.TenantId)?.Value is { } tenantId
                    ? RateLimitPartition.GetFixedWindowLimiter(tenantId, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = tenantPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                    : RateLimitPartition.GetNoLimiter("anonymous"));
        });

        return services;
    }

    public static IServiceCollection AddPlatformOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        // OutboxProcessor is generic — it knows nothing about any module beyond the
        // event-type map each module contributes in its own AddModule. Swapping this
        // for a real broker later only changes what happens after deserialization.
        services.Configure<OutboxProcessorOptions>(options =>
        {
            var section = configuration.GetSection("Outbox");
            options.Enabled = section.GetValue("Enabled", options.Enabled);
            options.BatchSize = section.GetValue("BatchSize", options.BatchSize);
            options.MaxAttempts = section.GetValue("MaxAttempts", options.MaxAttempts);
            if (section.GetValue<double?>("PollingIntervalSeconds") is { } seconds)
            {
                options.PollingInterval = TimeSpan.FromSeconds(seconds);
            }
        });
        services.AddHostedService<OutboxProcessor>();

        return services;
    }

    public static IServiceCollection AddPlatformHttp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        return services;
    }
}
