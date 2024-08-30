using CompatApiClient.Utils;
using DSharpPlus.Entities;
using IrdLibraryClient;
using IrdLibraryClient.POCOs;

namespace CompatBot.Utils.ResultFormatters;

public static class IrdSearchResultFormatter
{
    public static DiscordEmbedBuilder AsEmbed(this SearchResult? searchResult)
    {
        var result = new DiscordEmbedBuilder
        {
            Color = Config.Colors.DownloadLinks,
        };

        if (searchResult?.Data is null or { Count: 0 })
        {
            result.Color = Config.Colors.LogResultFailed;
            result.Description = "No matches were found";
            return result;
        }
        var irdClient = new IrdClient();
        foreach (var item in searchResult.Data)
        {
            if (string.IsNullOrEmpty(item.Filename))
                continue;

            string[] parts = item.Filename.Split('-');
            if (parts.Length == 1)
                parts = new[] { "", item.Filename };
            string downloadLink = irdClient.GetDownloadLink(item.Filename);
            result.AddField(
                $"[{parts[0]} v{item.GameVersion}] {item.Title?.Sanitize().Trim(EmbedPager.MaxFieldTitleLength)}",
                $"[⏬ `{parts[1].Sanitize().Trim(200)}`]({downloadLink})"
            );
        }
        return result;
    }
}