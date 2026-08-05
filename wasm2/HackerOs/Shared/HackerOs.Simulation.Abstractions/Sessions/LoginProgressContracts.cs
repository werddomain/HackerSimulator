namespace HackerOs.Simulation.Abstractions.Sessions;

/// <summary>Represents a snapshot of active login progress steps.</summary>
public sealed record LoginProgressSnapshot(
    string CurrentStepName,
    int CompletedSteps,
    int TotalSteps,
    int Percentage,
    IReadOnlyList<string> StepHistory);

/// <summary>Tracks login sequence progress using disposable step scopes.</summary>
public interface ILoginProgressTracker
{
    /// <summary>Gets the current progress snapshot.</summary>
    LoginProgressSnapshot CurrentSnapshot { get; }

    /// <summary>Event raised whenever login step progress is updated.</summary>
    event Action<LoginProgressSnapshot>? ProgressChanged;

    /// <summary>Sets the expected total number of steps in the login process.</summary>
    void SetTotalSteps(int totalSteps);

    /// <summary>Starts a new named login step that completes when the returned scope is disposed.</summary>
    IDisposable BeginStep(string stepName);

    /// <summary>Resets the tracker for a new login sequence.</summary>
    void Reset();
}

/// <summary>No-op default implementation of <see cref="ILoginProgressTracker"/>.</summary>
public sealed class NullLoginProgressTracker : ILoginProgressTracker
{
    public static NullLoginProgressTracker Instance { get; } = new();

    public LoginProgressSnapshot CurrentSnapshot => new("Ready", 0, 1, 0, []);

    public event Action<LoginProgressSnapshot>? ProgressChanged
    {
        add { }
        remove { }
    }

    public void SetTotalSteps(int totalSteps) { }

    public IDisposable BeginStep(string stepName) => NullScope.Instance;

    public void Reset() { }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
