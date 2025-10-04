namespace RssReader.Data.Models;

public sealed record RssFeed(
    ObjectId Id,
    string Url,
    string? Title,
    string? ImageUrl,
    string? Description,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt);
