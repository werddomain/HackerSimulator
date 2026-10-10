using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Tests.Support;

/// <summary>
/// In-memory <see cref="IAppEnvironmentGateway"/> test double: one mutable process environment
/// (ADR 0050), starting in <paramref name="homeDirectory"/> with <c>HOME</c> set.
/// </summary>
public sealed class FakeAppEnvironmentGateway(
    string homeDirectory,
    string userName = "alice",
    string machineName = "hackeros",
    IReadOnlyList<string>? arguments = null) : IAppEnvironmentGateway
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal) { ["HOME"] = homeDirectory };

    public string CurrentDirectory { get; private set; } = homeDirectory;

    public string UserName { get; } = userName;

    public string MachineName { get; set; } = machineName;

    public IReadOnlyList<string> GetCommandLineArgs() => arguments ?? ["test"];

    public void SetCurrentDirectory(VirtualPath directory) => CurrentDirectory = directory.Value;

    public string? GetVariable(string name) => _variables.TryGetValue(name, out string? value) ? value : null;

    public IReadOnlyDictionary<string, string> GetVariables() => new Dictionary<string, string>(_variables, StringComparer.Ordinal);

    public void SetVariable(string name, string? value)
    {
        ProcessEnvironmentRules.ValidateName(name);
        if (value is null)
        {
            _variables.Remove(name);
            return;
        }

        ProcessEnvironmentRules.ValidateValue(value);
        _variables[name] = value;
    }
}
