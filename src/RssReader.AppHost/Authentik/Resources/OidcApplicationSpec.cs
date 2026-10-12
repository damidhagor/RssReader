namespace RssReader.AppHost.Authentik.Resources;

internal sealed record OidcApplicationSpec(
    string Name,
    IReadOnlyList<ReferenceExpression> RedirectUris,
    IReadOnlyList<ReferenceExpression> LogoutRedirectUris);
