﻿using CompatBot.Commands;
using CompatBot.Database;
using Microsoft.EntityFrameworkCore;

namespace CompatBot.EventHandlers;

public static class UsernameValidationMonitor
{
    public static Task OnMemberUpdated(DiscordClient _, GuildMemberUpdatedEventArgs args) => UpdateDisplayName(args.Guild, args.Member);
    public static Task OnMemberAdded(DiscordClient _, GuildMemberAddedEventArgs args) => UpdateDisplayName(args.Guild, args.Member);

    private static async Task UpdateDisplayName(DiscordGuild guild, DiscordMember guildMember)
    {
        try
        {
            if (guildMember.IsWhitelisted())
                return;

            if (guild.Permissions?.HasFlag(DiscordPermission.ChangeNickname) is false)
                return;

            await using var db = await BotDb.OpenReadAsync().ConfigureAwait(false);
            var forcedNickname = await db.ForcedNicknames.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == guildMember.Id && x.GuildId == guildMember.Guild.Id).ConfigureAwait(false);
            if (forcedNickname is null)
                return;
                    
            if (guildMember.DisplayName == forcedNickname.Nickname)
                return;

            Config.Log.Debug($"Expected nickname {forcedNickname.Nickname}, but was {guildMember.Nickname}. Renaming…");
            await guildMember.ModifyAsync(mem => mem.Nickname = forcedNickname.Nickname).ConfigureAwait(false);
            Config.Log.Info($"Enforced nickname {forcedNickname.Nickname} for user {guildMember.Id} ({guildMember.Username}#{guildMember.Discriminator})");
        }
        catch (Exception e)
        {
            Config.Log.Error(e);
        }
    }

    public static async Task MonitorAsync(DiscordClient client, bool once = false)
    {
        do
        {
            if (!once)
                await Task.Delay(Config.ForcedNicknamesRecheckTimeInHours, Config.Cts.Token).ConfigureAwait(false);
            if (!await Audit.CheckLock.WaitAsync(0).ConfigureAwait(false))
                continue;
                
            try
            {
                foreach (var guild in client.Guilds.Values)
                    try
                    {
                        if (guild.Permissions?.HasFlag(DiscordPermission.ChangeNickname) is false)
                            continue;

                        await using var db = await BotDb.OpenReadAsync().ConfigureAwait(false);
                        var forcedNicknames = await db.ForcedNicknames
                            .Where(mem => mem.GuildId == guild.Id)
                            .ToListAsync()
                            .ConfigureAwait(false);
                        if (forcedNicknames.Count == 0)
                            continue;

                        foreach (var forced in forcedNicknames)
                        {
                            var member = await client.GetMemberAsync(guild, forced.UserId).ConfigureAwait(false);
                            if (member is null || member.DisplayName == forced.Nickname)
                                continue;
                                
                            try
                            {
                                await member.ModifyAsync(mem => mem.Nickname = forced.Nickname).ConfigureAwait(false);
                                Config.Log.Info($"Enforced nickname {forced.Nickname} for user {member.Id} ({member.Username}#{member.Discriminator})");
                            }
                            catch { }
                        }
                    }
                    catch (Exception e)
                    {
                        Config.Log.Error(e);
                    }
            }
            finally
            {
                Audit.CheckLock.Release();
            }
        } while (!Config.Cts.IsCancellationRequested && !once);
    }
}