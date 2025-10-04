namespace RssReader.Data;

internal static class LogEvents
{
    public const int AddRssFeedAdded = 2000;
    public const int AddRssFeedUpdated = 2001;
    public const int AddRssFeedFailed = 2002;

    public const int GetRssFeedByIdSuccess = 2002;
    public const int GetRssFeedByIdNotFound = 2003;
    public const int GetRssFeedByIdError = 2004;

    public const int GetRssFeedsByIdsSuccess = 2005;
    public const int GetRssFeedsByIdsError = 2006;

    public const int UpdateRssFeedSuccess = 2007;
    public const int UpdateRssFeedNotFound = 2008;
    public const int UpdateRssFeedError = 2009;

    public const int UpdateRssFeedSettingsSuccess = 2010;
    public const int UpdateRssFeedSettingsNotFound = 2011;
    public const int UpdateRssFeedSettingsError = 2012;
}
