using HackerOs.OS.User;

namespace HackerOs.OS.Applications;

/// <summary>
/// Context information for launching an application
/// </summary>
public class ApplicationLaunchContext
{
    /// <summary>
    /// User session that is launching the application
    /// </summary>
    public required UserSession UserSession { get; set; }

    /// <summary>
    /// Command line arguments for the application
    /// </summary>
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// Environment variables for the application
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Working directory for the application
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Files to open with the application (if any)
    /// </summary>
    public List<string> FilesToOpen { get; set; } = new();

    /// <summary>
    /// Window position for windowed applications
    /// </summary>
    public WindowPosition? WindowPosition { get; set; }

    /// <summary>
    /// Window size for windowed applications
    /// </summary>
    public WindowSize? WindowSize { get; set; }

    /// <summary>
    /// Whether to start the application minimized
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Whether to start the application maximized
    /// </summary>
    public bool StartMaximized { get; set; } = false;

    /// <summary>
    /// Priority level for the application process
    /// </summary>
    public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;

    /// <summary>
    /// Standard input stream for command-line applications
    /// </summary>
    public Stream? StandardInput { get; set; }

    /// <summary>
    /// Standard output stream for command-line applications
    /// </summary>
    public Stream? StandardOutput { get; set; }

    /// <summary>
    /// Standard error stream for command-line applications
    /// </summary>
    public Stream? StandardError { get; set; }

    /// <summary>
    /// Additional context data
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Parent process ID (if launched from another application)
    /// </summary>
    public int? ParentProcessId { get; set; }

    /// <summary>
    /// Whether to wait for the application to exit
    /// </summary>
    public bool WaitForExit { get; set; } = false;

    /// <summary>
    /// Timeout for application startup (in milliseconds)
    /// </summary>
    public int StartupTimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Create a basic launch context for a user session
    /// </summary>
    /// <param name="userSession">User session</param>
    /// <param name="arguments">Command line arguments</param>
    /// <returns>Launch context</returns>
    public static ApplicationLaunchContext Create(UserSession userSession, params string[] arguments)
    {
        return new ApplicationLaunchContext
        {
            UserSession = userSession,
            Arguments = arguments.ToList()
        };
    }

    /// <summary>
    /// Create a launch context for opening files
    /// </summary>
    /// <param name="userSession">User session</param>
    /// <param name="filesToOpen">Files to open</param>
    /// <returns>Launch context</returns>
    public static ApplicationLaunchContext CreateForFiles(UserSession userSession, params string[] filesToOpen)
    {
        return new ApplicationLaunchContext
        {
            UserSession = userSession,
            FilesToOpen = filesToOpen.ToList()
        };
    }

    /// <summary>
    /// Create a launch context for command-line applications with streams
    /// </summary>
    /// <param name="userSession">User session</param>
    /// <param name="stdin">Standard input stream</param>
    /// <param name="stdout">Standard output stream</param>
    /// <param name="stderr">Standard error stream</param>
    /// <param name="arguments">Command line arguments</param>
    /// <returns>Launch context</returns>
    public static ApplicationLaunchContext CreateForCommandLine(
        UserSession userSession,
        Stream? stdin = null,
        Stream? stdout = null,
        Stream? stderr = null,
        params string[] arguments)
    {
        return new ApplicationLaunchContext
        {
            UserSession = userSession,
            Arguments = arguments.ToList(),
            StandardInput = stdin,
            StandardOutput = stdout,
            StandardError = stderr
        };
    }
}

/// <summary>
/// Window position for windowed applications
/// </summary>
public class WindowPosition
{
    /// <summary>
    /// X coordinate of the window
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of the window
    /// </summary>
    public int Y { get; set; }

    public WindowPosition(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Window size for windowed applications
/// </summary>
public class WindowSize
{
    /// <summary>
    /// Width of the window
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the window
    /// </summary>
    public int Height { get; set; }

    public WindowSize(int width, int height)
    {
        Width = width;
        Height = height;
    }
}

/// <summary>
/// Process priority levels
/// </summary>
public enum ProcessPriority
{
    /// <summary>
    /// Lowest priority
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Below normal priority
    /// </summary>
    BelowNormal = 1,

    /// <summary>
    /// Normal priority
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Above normal priority
    /// </summary>
    AboveNormal = 3,

    /// <summary>
    /// High priority
    /// </summary>
    High = 4,

    /// <summary>
    /// Real-time priority (reserved for system)
    /// </summary>
    RealTime = 5
}
