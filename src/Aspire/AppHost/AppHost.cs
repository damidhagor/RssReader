var builder = DistributedApplication.CreateBuilder(args);

var mongodb = builder.AddMongoDB("mongodb")
                     .WithContainerName("rssreader-mongodb")
                     .WithDataVolume("rssreader-mongodb-data")
                     .WithLifetime(ContainerLifetime.Persistent)
                     .WithMongoExpress(containerName: "rssreader-mongo-express");

var api = builder.AddProject<Projects.RssReader_Api>("rssreader-api")
                 .WithReference(mongodb)
                 .WaitFor(mongodb);

builder.Build().Run();
