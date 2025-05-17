﻿namespace CompatBot.Commands;
#if DEBUG
internal sealed class DevOnly
{
    [Command("whitespacetest"), TextAlias("wst", "wstest")]
    [Description("Testing discord embeds breakage for whitespaces")]
    public async Task WhitespaceTest(CommandContext ctx)
    {
        var checkMark = "[\u00a0]";
        const int width = 20;
        var result = new StringBuilder($"` 1. Dots:{checkMark.PadLeft(width, '.')}`").AppendLine()
            .AppendLine($"` 2. None:{checkMark,width}`");
        var ln = 3;
        foreach (var c in StringUtils.SpaceCharacters)
            result.AppendLine($"`{ln++,2}. {(int)c:x4}:{checkMark,width}`");
#pragma warning disable 8321
        static void addRandomStuff(DiscordEmbedBuilder emb)
        {
            emb.AddField("Random section", """
                😾 lasjdf wqoieyr osdf `Vreoh Sdab` wohe `270`
                🤔 salfhiosfhsero hskfh shufwei oufhwehw e wkihrwe h
                ℹ️ sakfjas f hs `ASfhewighehw safds` asfw
                🔮 ¯\\\_(ツ)\_/¯
                """, false);
        }
#pragma warning restore 8321
        var embed = new DiscordEmbedBuilder()
            .WithTitle("Whitespace embed test")
            .WithDescription("In a perfect world all these lines would look the same, with perfectly formed columns");

        var lines = result.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var embedList = lines.BreakInEmbeds(embed, lines.Length / 2 + lines.Length % 2, "Normal");
        foreach (var _ in embedList)
        {
            //drain the enumerable
        }
        embed.AddField("-", "-", false);

        lines = result.ToString().Replace(' ', StringUtils.Nbsp).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        embedList = lines.BreakInEmbeds(embed, lines.Length / 2 + lines.Length % 2, "Non-breakable spaces");
        foreach (var _ in embedList)
        {
            //drain the enumerable
        }
        await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
    }

    [Command("buttons")]
    [Description("Buttons test")]
    public async Task Buttons(CommandContext ctx)
    {
        var builder = new DiscordMessageBuilder()
            .WithContent("Regular button vs emoji button")
            .AddActionRowComponent(
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "pt", "✅ Regular"),
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "pe", "Emoji", emoji: new(DiscordEmoji.FromUnicode("✅")))
            );
        await ctx.RespondAsync(builder).ConfigureAwait(false);
    }
}
#endif