using RssReader.AppHost.Authentik.Resources;

namespace RssReader.AppHost.Authentik;

internal static partial class AuthentikExtensions
{
    public static IResourceBuilder<AuthentikResource> WithUser(this IResourceBuilder<AuthentikResource> builder, string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var password = builder.ApplicationBuilder.AddParameter($"{builder.Resource.Name}-user-{username}-password", true);
        password.WithParentRelationship(builder);

        builder.Resource.Worker.Users.Add(new UserSpec(username, password.Resource));
        builder.EnsureBlueprints();

        return builder;
    }

    public static IResourceBuilder<AuthentikResource> WithOidcApplication(
        this IResourceBuilder<AuthentikResource> builder,
        string name,
        Action<OidcApplicationBuilder> configureApplication)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configureApplication);

        var oidcApplication = new OidcApplicationBuilder();
        configureApplication(oidcApplication);

        if (!oidcApplication.HasRedirectUri)
        {
            throw new ArgumentException(
                $"The OIDC application '{name}' must configure at least one authorization redirect URI via {nameof(OidcApplicationBuilder.WithRedirectUri)}.",
                nameof(configureApplication));
        }

        builder.Resource.Worker.OidcApplications.Add(oidcApplication.Build(name));
        builder.EnsureBlueprints();

        return builder;
    }
}
