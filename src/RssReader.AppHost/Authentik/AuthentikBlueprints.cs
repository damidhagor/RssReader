using System.Globalization;
using System.Text;
using RssReader.AppHost.Authentik.Resources;

namespace RssReader.AppHost.Authentik;

internal static class AuthentikBlueprints
{
    public static async Task<string> Render(
        IReadOnlyList<UserSpec> users,
        IReadOnlyList<OidcApplicationSpec> applications,
        CancellationToken cancellationToken)
    {
        var yaml = new StringBuilder();
        yaml.AppendLine(
            """
            version: 1
            metadata:
              name: RssReader - Aspire users and applications
            entries:
            """);

        foreach (var user in users)
        {
            var password = await user.PasswordParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            AppendUserEntry(yaml, user.Username, password ?? "");
        }

        foreach (var application in applications)
        {
            var redirectUris = await ResolveUris(application.RedirectUris, cancellationToken).ConfigureAwait(false);
            var logoutRedirectUris = await ResolveUris(application.LogoutRedirectUris, cancellationToken).ConfigureAwait(false);
            AppendOidcApplicationEntries(yaml, application.Name, redirectUris, logoutRedirectUris);
        }

        return yaml.ToString().ReplaceLineEndings("\n");
    }

    private static async Task<List<string>> ResolveUris(IReadOnlyList<ReferenceExpression> uris, CancellationToken cancellationToken)
    {
        var resolved = new List<string>(uris.Count);

        foreach (var uri in uris)
        {
            resolved.Add(await uri.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? "");
        }

        return resolved;
    }

    private static void AppendUserEntry(StringBuilder yaml, string username, string password)
    {
        var escapedUsername = EscapeYamlString(username);

        yaml.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
              - model: authentik_core.user
                id: user-{username}
                identifiers:
                  username: "{escapedUsername}"
                attrs:
                  name: "{escapedUsername}"
                  password: "{EscapeYamlString(password)}"
                  is_active: true
            """);
    }

    private static void AppendOidcApplicationEntries(
        StringBuilder yaml,
        string name,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> logoutRedirectUris)
    {
        var escapedName = EscapeYamlString(name);

        yaml.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
              - model: authentik_providers_oauth2.oauth2provider
                id: oidc-provider-{name}
                identifiers:
                  name: "{escapedName}"
                attrs:
                  client_type: public
                  client_id: "{escapedName}"
                  grant_types:
                    - authorization_code
                    - refresh_token
                  authorization_flow: !Find [authentik_flows.flow, [slug, default-provider-authorization-implicit-consent]]
                  invalidation_flow: !Find [authentik_flows.flow, [slug, default-provider-invalidation-flow]]
                  signing_key: !Find [authentik_crypto.certificatekeypair, [name, "authentik Self-signed Certificate"]]
                  redirect_uris:
            """);

        foreach (var redirectUri in redirectUris)
        {
            AppendRedirectUri(yaml, redirectUri, "authorization");
        }

        foreach (var logoutRedirectUri in logoutRedirectUris)
        {
            AppendRedirectUri(yaml, logoutRedirectUri, "logout");
        }

        yaml.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
                  property_mappings:
                    - !Find [authentik_providers_oauth2.scopemapping, [managed, goauthentik.io/providers/oauth2/scope-openid]]
                    - !Find [authentik_providers_oauth2.scopemapping, [managed, goauthentik.io/providers/oauth2/scope-email]]
                    - !Find [authentik_providers_oauth2.scopemapping, [managed, goauthentik.io/providers/oauth2/scope-profile]]
              - model: authentik_core.application
                id: oidc-application-{name}
                identifiers:
                  slug: "{escapedName}"
                attrs:
                  name: "{escapedName}"
                  provider: !KeyOf oidc-provider-{name}
            """);
    }

    private static void AppendRedirectUri(StringBuilder yaml, string url, string redirectUriType)
        => yaml.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
                    - matching_mode: strict
                      url: "{EscapeYamlString(url)}"
                      redirect_uri_type: {redirectUriType}
            """);

    private static string EscapeYamlString(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
