using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Sessions;

/// <summary>Default thread-safe implementation of <see cref="ILoginProgressTracker"/>.</summary>
public sealed class LoginProgressTracker : ILoginProgressTracker
{
    private readonly Lock _gate = new();
    private readonly List<string> _history = [];
    private string _currentStepName = "Initializing";
    private int _completedSteps;
    private int _totalSteps = 3;

    /// <inheritdoc />
    public event Action<LoginProgressSnapshot>? ProgressChanged;

    /// <inheritdoc />
    public LoginProgressSnapshot CurrentSnapshot
    {
        get
        {
            lock (_gate)
            {
                int percentage = _totalSteps > 0
                    ? Math.Min(100, (int)Math.Round((double)_completedSteps / _totalSteps * 100))
                    : 0;

                return new LoginProgressSnapshot(
                    _currentStepName,
                    _completedSteps,
                    _totalSteps,
                    percentage,
                    [.. _history]);
            }
        }
    }

    /// <inheritdoc />
    public void SetTotalSteps(int totalSteps)
    {
        lock (_gate)
        {
            _totalSteps = Math.Max(1, totalSteps);
        }

        NotifyProgressChanged();
    }

    /// <inheritdoc />
    public IDisposable BeginStep(string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        lock (_gate)
        {
            _currentStepName = stepName;
        }

        NotifyProgressChanged();
        return new StepScope(this, stepName);
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_gate)
        {
            _currentStepName = "Initializing";
            _completedSteps = 0;
            _history.Clear();
        }

        NotifyProgressChanged();
    }

    private void CompleteStep(string stepName)
    {
        lock (_gate)
        {
            _completedSteps++;
            _history.Add(stepName);
            int percentage = (int)Math.Round((double)_completedSteps / _totalSteps * 100);
            if (percentage >= 100)
            {
                _currentStepName = "Session Ready";
            }
        }

        NotifyProgressChanged();
    }

    private void NotifyProgressChanged()
    {
        ProgressChanged?.Invoke(CurrentSnapshot);
    }

    private sealed class StepScope(LoginProgressTracker tracker, string stepName) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            tracker.CompleteStep(stepName);
        }
    }
}
