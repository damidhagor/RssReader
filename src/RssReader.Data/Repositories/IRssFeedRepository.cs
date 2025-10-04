namespace RssReader.Data.Repositories;

public interface IRssFeedRepository
{
    Task<AddRssFeedResult> Add(RssFeed feed, CancellationToken cancellationToken);
}
