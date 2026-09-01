using RssReader.AppHost.Authentik;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithContainerName("rss-postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("rss-postgres-data")
    .WithUrlForEndpoint("tcp", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly);

postgres.WithDbGate(
    dbGate => dbGate
        .WithContainerName("rss-postgres-dbgate")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume("rss-postgres-dbgate-data")
        .WithParentRelationship(postgres),
    "postgres-dbgate");

var rssReaderDb = postgres.AddDatabase("postgres-rssreader", "rssreader");
var authentikDb = postgres.AddDatabase("postgres-authentik", "authentik");

builder.AddAuthentik(
    "authentik",
    authentikDb,
    server => server
        .WithContainerName("rss-authentik-server")
        .WithLifetime(ContainerLifetime.Persistent),
    worker => worker
        .WithContainerName("rss-authentik-worker")
        .WithLifetime(ContainerLifetime.Persistent))
    .WithMediaVolume("rss-authentik-media")
    .WithTemplatesVolume("rss-authentik-templates")
    .WithCertsVolume("rss-authentik-certs");

builder.Build().Run();
