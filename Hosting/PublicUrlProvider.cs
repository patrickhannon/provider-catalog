using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ProviderCatalog.Hosting;

/// <summary>
/// Provides the public URL for the catalog, using the tunnel URL if available,
/// or falling back to the local address.
/// </summary>
public sealed class PublicUrlProvider
{
    private string? _publicUrl;

    public void Initialize(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?
            .Addresses.FirstOrDefault()?.Replace("[::]", "localhost").Replace("0.0.0.0", "localhost")
            ?? "http://localhost:5180";
        _publicUrl = address;
    }

    public void SetTunnelUrl(string tunnelUrl)
    {
        _publicUrl = tunnelUrl;
    }

    public string GetPublicUrl() => _publicUrl ?? "http://localhost:5180";
}
