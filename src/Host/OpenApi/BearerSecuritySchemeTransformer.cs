using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Host.OpenApi;

/// <summary>Describes the API and its JWT bearer authentication in the generated OpenAPI document.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "OrderPoint API",
            Version = "v1",
            Description = "Multi-tenant SaaS reference API. Call POST /api/identity/tenants/register or /api/identity/auth/login, "
                + "then send the returned access token as 'Authorization: Bearer <token>'.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from the Identity module's register, login, or accept-invitation endpoints.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
