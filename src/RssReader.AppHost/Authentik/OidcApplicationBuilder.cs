using RssReader.AppHost.Authentik.Resources;

namespace RssReader.AppHost.Authentik;

internal sealed class OidcApplicationBuilder
{
    private readonly List<ReferenceExpression> _redirectUris = [];
    private readonly List<ReferenceExpression> _logoutRedirectUris = [];

    internal bool HasRedirectUri => _redirectUris.Count > 0;

    public OidcApplicationBuilder WithRedirectUri(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _redirectUris.Add(ReferenceExpression.Create($"{url}"));
        return this;
    }

    public OidcApplicationBuilder WithRedirectUri(EndpointReference endpoint, string relativePath = "/authentication/login-callback")
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        _redirectUris.Add(BuildUri(endpoint, relativePath));
        return this;
    }

    public OidcApplicationBuilder WithLogoutRedirectUri(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _logoutRedirectUris.Add(ReferenceExpression.Create($"{url}"));
        return this;
    }

    public OidcApplicationBuilder WithLogoutRedirectUri(EndpointReference endpoint, string relativePath = "/authentication/logout-callback")
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        _logoutRedirectUris.Add(BuildUri(endpoint, relativePath));
        return this;
    }

    private static ReferenceExpression BuildUri(EndpointReference endpoint, string relativePath)
        => ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.Url)}{relativePath}");

    internal OidcApplicationSpec Build(string name)
        => new(name, [.. _redirectUris], [.. _logoutRedirectUris]);
}
