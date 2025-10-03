using Aspire.MongoDB.Driver;

namespace RssReader.Data;

public static class HostingExtensions
{
    public static T AddRssReaderData<T>(
        this T builder,
        string connectionName,
        Action<MongoDBSettings>? configureSettings = null,
        Action<MongoClientSettings>? configureClientSettings = null)
        where T : IHostApplicationBuilder
    {
        builder.AddMongoDBClient(connectionName, configureSettings, configureClientSettings);

        return builder;
    }
}
