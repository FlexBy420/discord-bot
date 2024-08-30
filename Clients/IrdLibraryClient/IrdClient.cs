using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CompatApiClient;
using CompatApiClient.Compression;
using CompatApiClient.Utils;
using IrdLibraryClient.IrdFormat;
using IrdLibraryClient.POCOs;

namespace IrdLibraryClient;

public class IrdClient
{
    public static readonly string JsonUrl = "https://flexby420.github.io/playstation_3_ird_database/all.json";
    private readonly HttpClient client;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly Uri BaseDownloadUri = new("https://github.com/FlexBy420/playstation_3_ird_database/raw/main/");

    public IrdClient()
    {
        client = HttpClientFactory.Create(new CompressionMessageHandler());
        jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true,
        };
    }

    public async Task<SearchResult?> SearchAsync(string query, CancellationToken cancellationToken)
    {
        query = query.ToUpper();
        try
        {
            using var response = await client.GetAsync(JsonUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                ApiConfig.Log.Error($"Failed to fetch IRD data: {response.StatusCode}");
                return null;
            }

            var jsonResult = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var irdData = JsonSerializer.Deserialize<Dictionary<string, IrdInfo[]>>(jsonResult, jsonOptions);
            if (irdData == null)
            {
                ApiConfig.Log.Error("Failed to deserialize IRD JSON data.");
                return null;
            }

            if (irdData.TryGetValue(query, out var items))
            {
                var searchResults = items.Select(item => new SearchResultItem
                {
                    Id = query,
                    Title = item.Title,
                    GameVersion = item.GameVer ?? "Unknown",
                    UpdateVersion = item.AppVer ?? "Unknown",
                    IrdName = item.Link.Split('/').Last(),
                    Filename = $"{query}-{item.Link.Split('/').Last()}.ird",
                }).ToList();

                return new SearchResult { Data = searchResults };
            }

            return new SearchResult { Data = new List<SearchResultItem>() };
        }
        catch (Exception e)
        {
            ApiConfig.Log.Error(e);
            return null;
        }
    }

    private string GetDownloadLink(string irdFilename)
    {
        var builder = new UriBuilder(BaseDownloadUri)
        {
            Path = Path.Combine(Path.GetFileNameWithoutExtension(irdFilename), irdFilename)
        };
        return builder.ToString();
    }
}

public class IrdInfo
{
    public string Title { get; set; } = null!;
    public string? FwVer { get; set; }
    public string? GameVer { get; set; }
    public string? AppVer { get; set; }
    public string Link { get; set; } = null!;
}