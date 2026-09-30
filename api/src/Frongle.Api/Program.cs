using System.Security.Claims;
using Frongle.Api.Auth;
using Frongle.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FrongleDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Frongle")));

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
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .Build());

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<FrongleDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok()).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.MapGet("/api/hello", (ClaimsPrincipal user) => new
{
    Greeting = "Hello from Frongle",
    TenantId = user.FindFirstValue("tenant_id"),
    Roles = user.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray(),
});

app.Run();

public partial class Program;
