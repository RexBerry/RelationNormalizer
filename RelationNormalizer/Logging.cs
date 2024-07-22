namespace RelationNormalizer;

/// <summary>
/// Quickly thrown together implementation of logging.
/// </summary>
internal static class Logging
{
    /// <summary>
    /// The output to log to.
    /// </summary>
    public static StreamWriter Output { get; set; } =
        new(Console.OpenStandardOutput());

    /// <summary>
    /// The minimum log level to log.
    /// </summary>
    public static LogLevel LogLevel { get; set; } = LogLevel.Info;

    public static void Trace(string message)
    {
        if (!ShouldLog(LogLevel.Trace))
        {
            return;
        }

        Output.WriteLine($"\x1b[38;2;128;128;128m[TRACE] {message}\x1b[0m");
        Output.Flush();
    }

    public static void Debug(string message)
    {
        if (!ShouldLog(LogLevel.Debug))
        {
            return;
        }

        Output.WriteLine($"[DEBUG] {message}");
        Output.Flush();
    }

    public static void Info(string message)
    {
        if (!ShouldLog(LogLevel.Info))
        {
            return;
        }

        Output.WriteLine($"\x1b[38;2;100;200;255m[INFO] {message}\x1b[0m");
        Output.Flush();
    }

    public static void Warn(string message)
    {
        if (!ShouldLog(LogLevel.Warn))
        {
            return;
        }

        Output.WriteLine($"\x1b[38;2;255;200;50m[WARN] {message}\x1b[0m");
        Output.Flush();
    }

    public static void Error(string message)
    {
        if (!ShouldLog(LogLevel.Error))
        {
            return;
        }

        Output.WriteLine($"\x1b[38;2;255;100;100m[ERROR] {message}\x1b[0m");
        Output.Flush();
    }

    public static void Fatal(string message)
    {
        if (!ShouldLog(LogLevel.Fatal))
        {
            return;
        }

        Output.WriteLine($"\x1b[38;2;255;50;255m[FATAL] {message}\x1b[0m");
        Output.Flush();
    }

    /// <summary>
    /// Checks whether the given log level should be logged.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <returns>Whether to log a message of log level <c>level</c>.</returns>
    private static bool ShouldLog(LogLevel level) =>
        (int)level >= (int)LogLevel;
}
