﻿namespace CompatBot.Database.Providers;

internal static class ScrapeStateProvider
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(365);

    public static bool IsFresh(long timestamp)
        => IsFresh(new DateTime(timestamp, DateTimeKind.Utc));

    public static bool IsFresh(DateTime timestamp)
        => timestamp.Add(CheckInterval) > DateTime.UtcNow;

    public static async ValueTask<bool> IsFreshAsync(string locale, string? containerId = null)
    {
        var id = GetId(locale, containerId);
        await using var db = await ThumbnailDb.OpenReadAsync().ConfigureAwait(false);
        var timestamp = string.IsNullOrEmpty(id) ? db.State.OrderBy(s => s.Timestamp).FirstOrDefault() : db.State.FirstOrDefault(s => s.Locale == id);
        if (timestamp is { Timestamp: long checkDate and > 0 })
            return IsFresh(new DateTime(checkDate, DateTimeKind.Utc));
        return false;
    }

    public static async ValueTask<bool> IsFreshAsync(string locale, DateTime dataTimestamp)
    {
        await using var db = await ThumbnailDb.OpenReadAsync().ConfigureAwait(false);
        var timestamp = string.IsNullOrEmpty(locale) ? db.State.OrderBy(s => s.Timestamp).FirstOrDefault() : db.State.FirstOrDefault(s => s.Locale == locale);
        if (timestamp is { Timestamp: long checkDate and > 0 })
            return new DateTime(checkDate, DateTimeKind.Utc) > dataTimestamp;
        return false;
    }

    public static async ValueTask SetLastRunTimestampAsync(string locale, string? containerId = null)
    {
        if (string.IsNullOrEmpty(locale))
            throw new ArgumentException("Locale is mandatory", nameof(locale));

        var id = GetId(locale, containerId);
        await using var db = await ThumbnailDb.OpenWriteAsync().ConfigureAwait(false);
        var timestamp = db.State.FirstOrDefault(s => s.Locale == id);
        if (timestamp is null)
            await db.State.AddAsync(new() {Locale = id, Timestamp = DateTime.UtcNow.Ticks}).ConfigureAwait(false);
        else
            timestamp.Timestamp = DateTime.UtcNow.Ticks;
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async ValueTask CleanAsync(CancellationToken cancellationToken)
    {
        await using var db = await ThumbnailDb.OpenWriteAsync().ConfigureAwait(false);
        var latestTimestamp = db.State.OrderByDescending(s => s.Timestamp).FirstOrDefault()?.Timestamp;
        if (!latestTimestamp.HasValue)
            return;

        var cutOff = new DateTime(latestTimestamp.Value, DateTimeKind.Utc).Add(-CheckInterval);
        var oldItems = db.State.Where(s => s.Timestamp < cutOff.Ticks);
        db.State.RemoveRange(oldItems);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GetId(string locale, string? containerId)
        => string.IsNullOrEmpty(containerId) ? locale : $"{locale} - {containerId}";
}