using RssReader.SourceGeneration.Unions;

namespace RssReader.Data.Results;

[GenerateUnion<Added<RssFeed>, Updated<RssFeed>, Error>]
public partial struct AddRssFeedResult;
