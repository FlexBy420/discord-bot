﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CompatApiClient.POCOs;
#nullable disable
    
public class CompatResult
{
    public CompatApiStatus ReturnCode;
    public string SearchTerm;
    public Dictionary<string, TitleInfo> Results;

    [JsonIgnore]
    public TimeSpan RequestDuration;
    [JsonIgnore]
    public RequestBuilder RequestBuilder;
}

public class TitleInfo
{
    public static readonly TitleInfo Maintenance = new() { Status = "Maintenance" };
    public static readonly TitleInfo CommunicationError = new() { Status = "Error" };
    public static readonly TitleInfo Unknown = new() { Status = "Unknown" };

    public string Title;
    [JsonPropertyName("alternative-title")]
    public string AlternativeTitle;
    [JsonPropertyName("wiki-title")]
    public string WikiTitle;
    public string Status;
    public string Date;
    public int Thread;
    public string Commit;
    public int? Pr;
    public int? Network;
    public string Update;
    public bool? UsingLocalCache;
}
    
#nullable restore