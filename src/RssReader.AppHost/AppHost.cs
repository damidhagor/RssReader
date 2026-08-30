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

builder.Build().Run();
