﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CompatBot.EventHandlers.LogParsing.ArchiveHandlers;
using DSharpPlus.Entities;
using CompatBot.Utils;
using System.IO.Pipelines;
using System.Net.Http;
using System.Text;
using System.Threading;
using MediafireClient;

namespace CompatBot.EventHandlers.LogParsing.SourceHandlers
{
    internal sealed class MediafireHandler : BaseSourceHandler
    {
        //http://www.mediafire.com/file/tmybrjpmtrpcejl/DemonsSouls_CrashLog_Nov.19th.zip/file
        private static readonly Regex ExternalLink = new(@"(?<mediafire_link>(https?://)?(www\.)?mediafire\.com/file/(?<quick_key>[^/\s]+)/(?<filename>[^/\?\s]+)(/file)?)", DefaultOptions);
        private static readonly Client Client = new();

        public override async Task<(ISource? source, string? failReason)> FindHandlerAsync(DiscordMessage message, ICollection<IArchiveHandler> handlers)
        {
            if (string.IsNullOrEmpty(message.Content))
                return (null, null);

            var matches = ExternalLink.Matches(message.Content);
            if (matches.Count == 0)
                return (null, null);

            using var client = HttpClientFactory.Create();
            foreach (Match m in matches)
            {
                if (m.Groups["mediafire_link"].Value is string lnk
                    && !string.IsNullOrEmpty(lnk)
                    && Uri.TryCreate(lnk, UriKind.Absolute, out var webLink))
                {
                    try
                    {
                        var filename = m.Groups["filename"].Value;
                        var filesize = -1;

                        var directLink = await Client.GetDirectDownloadLinkAsync(webLink, Config.Cts.Token).ConfigureAwait(false);
                        if (directLink is null)
                            return (null, null);

                        using (var request = new HttpRequestMessage(HttpMethod.Head, directLink))
                        {
                            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Config.Cts.Token);
                            if (response.Content.Headers.ContentLength > 0)
                                filesize = (int)response.Content.Headers.ContentLength.Value;
                            if (response.Content.Headers.ContentDisposition?.FileName is string fname && !string.IsNullOrEmpty(fname))
                                filename = fname;
                        }

                        await using var stream = await client.GetStreamAsync(directLink).ConfigureAwait(false);
                        //var buf = BufferPool.Rent(SnoopBufferSize);
                        var buf = BufferPool.Rent(512*1024);
                        try
                        {
                            var read = await stream.ReadBytesAsync(buf).ConfigureAwait(false);
                            foreach (var handler in handlers)
                            {
                                var (canHandle, reason) = handler.CanHandle(filename, filesize, buf.AsSpan(0, read));
                                if (canHandle)
                                    return (new MediafireSource(directLink, handler, filename, filesize), null);
                                else if (!string.IsNullOrEmpty(reason))
                                    return (null, reason);
                            }
                            Config.Log.Debug("MediaFire Response:\n" + Encoding.UTF8.GetString(buf, 0, read));
                        }
                        finally
                        {
                            BufferPool.Return(buf);
                        }
                    }
                    catch (Exception e)
                    {
                        Config.Log.Warn(e, $"Error sniffing {m.Groups["mediafire_link"].Value}");
                    }
                }
            }
            return (null, null);
        }

        private sealed class MediafireSource : ISource
        {
            private readonly Uri? uri;
            private readonly IArchiveHandler handler;

            public string SourceType => "Mediafire";
            public string FileName { get; }
            public long SourceFileSize { get; }
            public long SourceFilePosition => handler.SourcePosition;
            public long LogFileSize => handler.LogSize;

            internal MediafireSource(Uri? uri, IArchiveHandler handler, string fileName, int fileSize)
            {
                this.uri = uri;
                this.handler = handler;
                FileName = fileName;
                SourceFileSize = fileSize;
            }

            public async Task FillPipeAsync(PipeWriter writer, CancellationToken cancellationToken)
            {
                using var client = HttpClientFactory.Create();
                await using var stream = await client.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
                await handler.FillPipeAsync(stream, writer, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
