namespace RssReader.Data.Repositories;

internal sealed partial class RssFeedRepository(
    ILogger<RssFeedRepository> logger,
    TimeProvider timeProvider,
    IMongoClient mongoClient)
    : IRssFeedRepository
{
    private readonly ILogger<RssFeedRepository> _logger = logger;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMongoCollection<RssFeed> _collection = mongoClient
        .GetDatabase(MongoDBConstants.DatabaseName)
        .GetCollection<RssFeed>(MongoDBConstants.RssFeedsCollectionName);

    public async Task<AddRssFeedResult> Add(RssFeed feed, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = _timeProvider.GetUtcNow();

            feed = await _collection.FindOneAndUpdateAsync(
                f => f.Url == feed.Url,
                Builders<RssFeed>.Update
                    .Set(f => f.Url, feed.Url)
                    .Set(f => f.Title, feed.Title)
                    .Set(f => f.ImageUrl, feed.ImageUrl)
                    .Set(f => f.Description, feed.Description)
                    .Set(f => f.UpdatedAt, timestamp)
                    .SetOnInsert(f => f.CreatedAt, timestamp),
                new() { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (feed.CreatedAt == timestamp)
            {
                LogRssFeedAdded(feed.Url, feed);
                return new Added<RssFeed>(feed);
            }
            else
            {
                LogRssFeedUpdated(feed.Url, feed);
                return new Updated<RssFeed>(feed);
            }
        }
        catch (Exception e)
        {
            LogAddFeedFailed(feed.Url, feed, e);
            return new Error(e);
        }
    }

    [LoggerMessage(
        LogEvents.AddRssFeedAdded,
        LogLevel.Information,
        "Added the rss feed from url '{Url}'.")]
    partial void LogRssFeedAdded(string url, [LogProperties] RssFeed feed);

    [LoggerMessage(
        LogEvents.AddRssFeedUpdated,
        LogLevel.Information,
        "Updated the rss feed from url '{Url}'.")]
    partial void LogRssFeedUpdated(string url, [LogProperties] RssFeed feed);

    [LoggerMessage(
        LogEvents.AddRssFeedFailed,
        LogLevel.Error,
        "Adding the rss feed from url '{Url}' failed.")]
    partial void LogAddFeedFailed(string url, [LogProperties] RssFeed feed, Exception e);
}
