using System.Globalization;

namespace HackerOs.Apps.SystemMonitor.Metrics;

/// <summary>Human-readable sizes, rates and durations, in binary units like <c>free -h</c>.</summary>
public static class Units
{
    private static readonly string[] ByteUnits = ["B", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>Formats a byte count (<c>1.5 MiB</c>).</summary>
    /// <param name="bytes">Byte count.</param>
    public static string Bytes(double bytes)
    {
        if (!double.IsFinite(bytes) || bytes < 0)
        {
            bytes = 0;
        }

        int unit = 0;
        while (bytes >= 1024 && unit < ByteUnits.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        string format = unit == 0 || bytes >= 100 ? "0" : "0.#";
        return $"{bytes.ToString(format, CultureInfo.InvariantCulture)} {ByteUnits[unit]}";
    }

    /// <summary>Formats a throughput (<c>12 KiB/s</c>).</summary>
    /// <param name="bytesPerSecond">Bytes per second.</param>
    public static string Rate(double bytesPerSecond) => $"{Bytes(bytesPerSecond)}/s";

    /// <summary>Formats a percentage with one decimal (<c>12.5%</c>).</summary>
    /// <param name="percent">Value in percent.</param>
    public static string Percent(double percent) =>
        $"{(double.IsFinite(percent) ? percent : 0).ToString("0.0", CultureInfo.InvariantCulture)}%";

    /// <summary>Formats a count with thousands separators (<c>12,345</c>).</summary>
    /// <param name="value">Count.</param>
    public static string Count(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>Formats an elapsed time like <c>uptime</c> (<c>2d 03:14:15</c>).</summary>
    /// <param name="duration">Elapsed time.</param>
    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        string clock = duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        return duration.Days > 0 ? $"{duration.Days.ToString(CultureInfo.InvariantCulture)}d {clock}" : clock;
    }
}
