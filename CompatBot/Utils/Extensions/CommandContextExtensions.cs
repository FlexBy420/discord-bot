﻿using System.Text.RegularExpressions;
using CompatBot.EventHandlers;
using DSharpPlus.Commands.Converters;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace CompatBot.Utils;

public static partial class CommandContextExtensions
{
    [GeneratedRegex(
        @"(?:https?://)?discord(app)?\.com/channels/(?<guild>\d+)/(?<channel>\d+)/(?<message>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline
    )]
    internal static partial Regex MessageLinkPattern();

    public static async Task<DiscordMember?> ResolveMemberAsync(this CommandContext ctx, string word)
    {
        try
        {
            if (ctx is not TextCommandContext tctx)
                return null;

            await using var scope = tctx.Extension.ServiceProvider.CreateAsyncScope();
            var cctx = new TextConverterContext
            {
                User = tctx.User,
                Channel = tctx.Channel,
                Message = tctx.Message,
                Command = tctx.Command,
                RawArguments = word,
                
                PrefixLength = tctx.Prefix?.Length ?? 0,
                Splicer = DefaultTextArgumentSplicer.Splice,
                Extension = tctx.Extension,
                ServiceScope = scope,
            };
            var result = await new DiscordMemberConverter().ConvertAsync(cctx).ConfigureAwait(false);
            return result.HasValue ? result.Value : null;
        }
        catch (Exception e)
        {
            Config.Log.Warn(e, $"Failed to resolve member {word}");
            return null;
        }
    }

    public static async Task<DiscordChannel> CreateDmAsync(this CommandContext ctx)
        => ctx.Channel.IsPrivate || ctx.Member is null ? ctx.Channel : await ctx.Member.CreateDmChannelAsync().ConfigureAwait(false);

    public static Task<DiscordChannel> GetChannelForSpamAsync(this CommandContext ctx)
        => ctx.Channel.IsSpamChannel() ? Task.FromResult(ctx.Channel) : ctx.CreateDmAsync();

    public static async Task<DiscordMessage?> GetMessageAsync(this CommandContext ctx, string messageLink)
    {
        if (MessageLinkPattern().Match(messageLink) is Match m
            && ulong.TryParse(m.Groups["guild"].Value, out var guildId)
            && ulong.TryParse(m.Groups["channel"].Value, out var channelId)
            && ulong.TryParse(m.Groups["message"].Value, out var msgId)
            && ctx.Client.Guilds.TryGetValue(guildId, out var guild)
            && await guild.GetChannelAsync(channelId).ConfigureAwait(false) is DiscordChannel channel)
            return await channel.GetMessageCachedAsync(msgId).ConfigureAwait(false)
                   ?? await channel.GetMessageAsync(msgId);
        return null;
    }

    public static async Task<DiscordMessage?> GetMessageAsync(this DiscordClient client, DiscordChannel channel, ulong messageId)
        => await channel.GetMessageCachedAsync(messageId).ConfigureAwait(false)
           ?? await channel.GetMessageAsync(messageId).ConfigureAwait(false);
}