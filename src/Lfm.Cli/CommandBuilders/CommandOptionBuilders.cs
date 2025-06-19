using System.CommandLine;

namespace Lfm.Cli.CommandBuilders;

public static class CommandOptionBuilders
{
    public static Option<bool> BuildForceCacheOption()
    {
        var option = new Option<bool>("--force-cache", "Use cached data regardless of expiry time");
        option.AddAlias("-fc");
        return option;
    }

    public static Option<bool> BuildForceApiOption()
    {
        var option = new Option<bool>("--force-api", "Always call API and cache result, ignore existing cache");
        option.AddAlias("-fa");
        return option;
    }

    public static Option<bool> BuildNoCacheOption()
    {
        var option = new Option<bool>("--no-cache", "Disable caching entirely for this request");
        option.AddAlias("-nc");
        return option;
    }

    public static (Option<bool> forceCache, Option<bool> forceApi, Option<bool> noCache) BuildCacheOptions()
    {
        return (BuildForceCacheOption(), BuildForceApiOption(), BuildNoCacheOption());
    }
}