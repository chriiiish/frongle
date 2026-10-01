using System.Security.Claims;
using System.Text.Json.Serialization;
using Frongle.Api;
using Frongle.Api.Areas;
using Frongle.Api.Assets;
using Frongle.Api.Auth;
using Frongle.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddOptions<StorageOptions>().BindConfiguration("Storage");
builder.Services.AddSingleton<IImageStorage, S3ImageStorage>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICaller, HttpCaller>();
builder.Services.AddScoped<TenantConnectionInterceptor>();
builder.Services.AddDbContext<FrongleDbContext>((provider, options) => options
    .UseFrongleNpgsql(builder.Configuration.GetConnectionString("Frongle"))
    .AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>()));

builder.Services.AddHealthChecks().AddDbContextCheck<FrongleDbContext>("database", tags: ["ready"]);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        // Inside the cluster the API reads Keycloak metadata from the service URL, not the public URL.
        options.MetadataAddress = builder.Configuration["Authentication:MetadataAddress"] ?? options.MetadataAddress;
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        options.TokenValidationParameters.ValidateAudience = false;
    });
builder.Services.AddTransient<IClaimsTransformation, KeycloakRolesClaimsTransformation>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AreaEndpoints.ManagerPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .RequireRole(Roles.MaintenanceManager))
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .Build());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Frongle API",
        Version = "v1",
        Description = """
            Frongle tracks assets such as light-posts, street signs, telephone poles, and traffic lights, and their maintenance and replacement.

            **Sign in.** Select **Authorize** and sign in with your Frongle user. The page uses Keycloak, the same sign-in as the web app, so the same users and roles apply.

            **Tenants.** Every request needs a token that carries a tenant. The API takes the tenant from the token and never from the request, so you see only your own organization's data.
            """,
        Contact = new OpenApiContact { Name = "Frongle", Url = new Uri("https://github.com/chriiiish/frongle") },
    });

    // These are the public Keycloak URLs, because the browser calls them.
    var realmUrl = builder.Configuration["Authentication:Authority"]!.TrimEnd('/');
    options.AddSecurityDefinition(KeycloakSecurityOperationFilter.SchemeName, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "Sign in with Keycloak. This is the same sign-in as the web app.",
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{realmUrl}/protocol/openid-connect/auth"),
                TokenUrl = new Uri($"{realmUrl}/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string> { ["openid"] = "Sign in and read your tenant and roles." },
            },
        },
    });
    options.OperationFilter<KeycloakSecurityOperationFilter>();
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<FrongleDbContext>().Database.Migrate();
}

// Under /api because the ingress sends only /api/* to this service. These middlewares run before
// authorization, so the spec and the page are public. They list the endpoints but expose no data.
app.UseSwagger(options => options.RouteTemplate = "api/swagger/{documentName}/swagger.json");
app.UseStaticFiles();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/swagger";
    options.DocumentTitle = "Frongle API";
    options.SwaggerEndpoint("v1/swagger.json", "Frongle API v1");
    // The page signs in as the web app does: the same Keycloak client, with PKCE and no client secret.
    options.OAuthClientId(app.Configuration["Authentication:ClientId"] ?? "frongle-web");
    options.OAuthUsePkce();
    options.OAuthAppName("Frongle API");
    options.HeadContent = """
        <link rel="stylesheet" href="/api/swagger/frongle.css" />
        <link rel="icon" href="/api/swagger/logo.svg" type="image/svg+xml" />
        <script>
          // The Frongle brand has one light theme, so keep Swagger UI out of its dark mode.
          new MutationObserver(function () {
            if (document.documentElement.classList.contains('dark-mode')) {
              document.documentElement.classList.remove('dark-mode');
            }
          }).observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
        </script>
        """;
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok())
    .AllowAnonymous()
    .WithTags("Health")
    .WithSummary("Check that the API is running")
    .WithDescription("Returns 200 when the process is up. It does not check the database. Use /health/ready for that.")
    .Produces(StatusCodes.Status200OK);
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(
        user.FindFirstValue("name") ?? user.FindFirstValue("preferred_username"),
        user.FindFirstValue("tenant_id")))
    .WithTags("Me")
    .WithSummary("Show who you are")
    .WithDescription("Returns your full name from your token, or your username when the token has no name, and your tenant. Use it to check that signing in works.")
    .Produces<MeResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden);

app.MapAreaEndpoints();
app.MapAssetEndpoints();
app.MapEventEndpoints();
app.MapImageEndpoints();

app.Run();

public partial class Program;
