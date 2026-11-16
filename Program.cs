using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ProviderCatalog.Auth;
using ProviderCatalog.Catalog;
using ProviderCatalog.Hosting;
using ProviderCatalog.Mcp;

var builder = WebApplication.CreateBuilder(args);

// Tokens and the dev tunnel id the catalog saves for you live in catalog.local.json.
builder.Configuration.AddJsonFile(LocalSettings.FileName, optional: true);
LocalSettings.EnsureTokens(builder.Configuration, builder.Environment.ContentRootPath);

if (builder.Configuration.GetValue<bool>("Catalog:Tunnel"))
{
    builder.Services.AddHostedService<DevTunnel>();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<StartupMessage>();
builder.Services.AddSingleton<SpecStore>();
builder.Services.AddSingleton<FeedbackLog>();
builder.Services.AddSingleton<AccessTokens>();
builder.Services.AddSingleton<CatalogTools>();
builder.Services.AddSingleton<McpServer>();
builder.Services.AddSingleton<PublicUrlProvider>();

// People sign in to the web pages once with their token and get a cookie.
// MCP clients send the token on every request in the Authorization header.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/sign-in";
        options.Cookie.Name = "provider-catalog";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddScheme<AuthenticationSchemeOptions, AccessTokenHandler>(AccessTokenHandler.SchemeName, configureOptions: null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("mcp", policy => policy
        .AddAuthenticationSchemes(AccessTokenHandler.SchemeName)
        .RequireAuthenticatedUser());

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/SignIn");
});

var app = builder.Build();

var startupMessage = app.Services.GetRequiredService<StartupMessage>();
app.Lifetime.ApplicationStarted.Register(() => startupMessage.Write(app));

var urlProvider = app.Services.GetRequiredService<PublicUrlProvider>();
app.Lifetime.ApplicationStarted.Register(() => urlProvider.Initialize(app));

app.UseAuthentication();
app.UseAuthorization();

// OAuth/OIDC metadata endpoint for MCP discovery
app.MapGet(".well-known/openid-configuration", (PublicUrlProvider urlProvider) =>
{
    var baseUrl = urlProvider.GetPublicUrl();
    return Results.Json(new
    {
        issuer = baseUrl,
        authorization_endpoint = $"{baseUrl}/signin",
        token_endpoint = $"{baseUrl}/token",
        userinfo_endpoint = $"{baseUrl}/userinfo",
        introspection_endpoint = $"{baseUrl}/introspect",
        response_types_supported = new[] { "code", "token" },
        subject_types_supported = new[] { "public" },
        id_token_signing_alg_values_supported = new[] { "RS256" },
        scopes_supported = new[] { "openid", "profile", "email" },
        token_endpoint_auth_methods_supported = new[] { "none", "client_secret_basic" },
        claims_supported = new[] { "sub", "name", "email" },
    });
}).AllowAnonymous().WithName("OpenIdConfig");

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapMcp("/mcp").RequireAuthorization("mcp");

app.Run();
