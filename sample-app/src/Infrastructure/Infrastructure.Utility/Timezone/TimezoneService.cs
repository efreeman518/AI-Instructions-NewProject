using NodaTime;
using NodaTime.TimeZones;

namespace Infrastructure.Utility.Timezone;

public static class TimezoneService
{
    public static DateTimeOffset ConvertToTimezone(DateTimeOffset utcDateTime, string timezoneId)
    {
        var tz = DateTimeZoneProviders.Tzdb[timezoneId];
        var instant = Instant.FromDateTimeOffset(utcDateTime);
        var zonedDateTime = instant.InZone(tz);
        return zonedDateTime.ToDateTimeOffset();
    }

    public static IReadOnlyList<string> GetAvailableTimezones()
    {
        return DateTimeZoneProviders.Tzdb.Ids.ToList().AsReadOnly();
    }

    public static string GetTimezoneDisplayName(string timezoneId)
    {
        var tz = DateTimeZoneProviders.Tzdb[timezoneId];
        var now = SystemClock.Instance.GetCurrentInstant();
        var offset = tz.GetUtcOffset(now);
        return $"(UTC{offset}) {timezoneId}";
    }
}
