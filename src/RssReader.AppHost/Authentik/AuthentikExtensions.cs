using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RssReader.AppHost.Authentik.Resources;

namespace RssReader.AppHost.Authentik;

internal static partial class AuthentikExtensions
{
    public static IResourceBuilder<AuthentikResource> AddAuthentik(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<PostgresDatabaseResource> database,
        Action<IResourceBuilder<AuthentikServerResource>>? configureServer = null,
        Action<IResourceBuilder<AuthentikWorkerResource>>? configureWorker = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        var adminPassword = builder.AddParameter($"{name}-akadmin-password", true);
        var secretKey = builder.AddParameter(
            $"{name}-secret-key",
            new GenerateParameterDefault { MinLength = 50 },
            true,
            true);
        var adminToken = builder.AddParameter(
            $"{name}-akadmin-token",
            new GenerateParameterDefault { MinLength = 32 },
            true,
            true);

        var server = builder.AddResource(new AuthentikServerResource($"{name}-server"))
            .WithImage("goauthentik/server")
            .WithImageRegistry("ghcr.io")
            .WithImageTag("2026.8.0")
            .WithIconName("Server", IconVariant.Regular)
            .WithArgs("server")
            .WithHttpsEndpoint(9443, 9443, "https")
            .WithHttpHealthCheck("/-/health/ready/", 200, "https")
            .WithEnvironment("AUTHENTIK_LISTEN__HTTP", "127.0.0.1:9000")
            .WithEnvironment("AUTHENTIK_SECRET_KEY", secretKey)
            .WithPostgresSql(database)
            .WithUrlForEndpoint("https", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrlForEndpoint("https", _ => new() { Url = "/", DisplayText = "Web" })
            .WithUrlForEndpoint("https", _ => new() { Url = "/api/v3/", DisplayText = "OpenApi" })
            .WaitFor(database);

        configureServer?.Invoke(server);

        var worker = builder.AddResource(new AuthentikWorkerResource($"{name}-worker"))
            .WithImage("goauthentik/server")
            .WithImageRegistry("ghcr.io")
            .WithImageTag("2026.8.0")
            .WithIconName("Server", IconVariant.Regular)
            .WithArgs("worker")
            .WithHttpEndpoint(targetPort: 9000, name: "http")
            .WithHttpHealthCheck("/-/health/ready/", 200, "http")
            .WithEnvironment("AUTHENTIK_SECRET_KEY", secretKey)
            .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", adminPassword)
            .WithEnvironment("AUTHENTIK_BOOTSTRAP_TOKEN", adminToken)
            .WithEnvironment("AUTHENTIK_WEB__BASE_URL", server.Resource.AuthorityExpression)
            .WithPostgresSql(database)
            .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithDevCertificate()
            .WaitFor(database);

        configureWorker?.Invoke(worker);

        server.WaitFor(worker);

        var authentik = builder.AddResource(
            new AuthentikResource(
                name,
                server.Resource,
                worker.Resource,
                secretKey.Resource,
                adminPassword.Resource,
                adminToken.Resource))
            .WithIconName("ShieldKeyhole", IconVariant.Regular)
            .WithInitialState(new()
            {
                ResourceType = "Authentik",
                Properties = [],
                State = KnownResourceStates.Waiting,
            });

        server.WithParentRelationship(authentik);
        worker.WithParentRelationship(authentik);
        secretKey.WithParentRelationship(authentik);
        adminPassword.WithParentRelationship(authentik);
        adminToken.WithParentRelationship(authentik);

        authentik.OnInitializeResource((resource, context, cancellationToken) =>
        {
            Task.Run(() => AuthentikHealthMonitor.Run(resource, context.Notifications, cancellationToken), cancellationToken);
            return Task.CompletedTask;
        });

        return authentik;
    }

    public static IResourceBuilder<AuthentikResource> WithImageTag(this IResourceBuilder<AuthentikResource> builder, string tag)
    {
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Server).WithImageTag(tag);
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Worker).WithImageTag(tag);
        return builder;
    }

    public static IResourceBuilder<AuthentikResource> WithMediaVolume(this IResourceBuilder<AuthentikResource> builder, string name)
    {
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Server).WithVolume(name, "/media");
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Worker).WithVolume(name, "/media");
        return builder;
    }

    public static IResourceBuilder<AuthentikResource> WithTemplatesVolume(this IResourceBuilder<AuthentikResource> builder, string name)
    {
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Server).WithVolume(name, "/templates");
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Worker).WithVolume(name, "/templates");
        return builder;
    }

    public static IResourceBuilder<AuthentikResource> WithCertsVolume(this IResourceBuilder<AuthentikResource> builder, string name)
    {
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Worker).WithVolume(name, "/certs");
        return builder;
    }

    private static IResourceBuilder<T> WithPostgresSql<T>(this IResourceBuilder<T> builder, IResourceBuilder<PostgresDatabaseResource> database)
        where T : ContainerResource
    {
        var postgres = database.Resource.Parent;

        return builder
            .WithEnvironment("AUTHENTIK_POSTGRESQL__HOST", postgres.PrimaryEndpoint.Property(EndpointProperty.Host))
            .WithEnvironment("AUTHENTIK_POSTGRESQL__PORT", postgres.PrimaryEndpoint.Property(EndpointProperty.Port))
            .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", database.Resource.DatabaseName)
            .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", postgres.UserNameReference)
            .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", postgres.PasswordParameter);
    }

    private static IResourceBuilder<AuthentikWorkerResource> WithDevCertificate(this IResourceBuilder<AuthentikWorkerResource> worker)
    {
        var blueprintPath = Path.Combine(worker.ApplicationBuilder.AppHostDirectory, "Authentik", "Blueprints", "aspire-dev-cert.yaml");

        return worker
            .WithBindMount(blueprintPath, "/blueprints/custom/aspire-dev-cert.yaml", true)
            .WithEnvironment(context =>
            {
#pragma warning disable ASPIRECERTIFICATES001 // Developer certificate trust APIs are experimental.
                var developerCertificateService = context.ExecutionContext.Services.GetRequiredService<IDeveloperCertificateService>();
#pragma warning restore ASPIRECERTIFICATES001

                var certificate = developerCertificateService.Certificates.FirstOrDefault();
                if (certificate is null)
                {
                    context.Logger.LogDevCertificateNotFound();
                    return;
                }

                context.EnvironmentVariables["AUTHENTIK_ASPIRE_DEV_CERT_PEM"] = certificate.ExportCertificatePem();
                context.EnvironmentVariables["AUTHENTIK_ASPIRE_DEV_CERT_KEY_PEM"] = ExportDevCertificatePrivateKeyPem(certificate);
            });
    }

    private static string ExportDevCertificatePrivateKeyPem(X509Certificate2 certificate)
    {
        using var rsaKey = certificate.GetRSAPrivateKey();
        if (rsaKey is not null)
        {
            return rsaKey.ExportPkcs8PrivateKeyPem();
        }

        using var ecdsaKey = certificate.GetECDsaPrivateKey();
        return ecdsaKey?.ExportPkcs8PrivateKeyPem()
            ?? throw new InvalidOperationException("The developer certificate does not have an exportable RSA or ECDSA private key.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No trusted ASP.NET Core developer certificate found; Authentik will keep using its own self-signed certificate.")]
    private static partial void LogDevCertificateNotFound(this ILogger logger);
}
