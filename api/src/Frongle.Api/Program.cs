using System.Security.Claims;
using Frongle.Api.Auth;
using Frongle.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Frongle API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "A Keycloak access token.",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
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
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/swagger";
    options.SwaggerEndpoint("v1/swagger.json", "Frongle API v1");
});

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
