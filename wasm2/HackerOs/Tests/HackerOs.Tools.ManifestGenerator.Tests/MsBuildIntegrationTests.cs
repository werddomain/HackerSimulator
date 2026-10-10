using System.Diagnostics;
using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestGenerator.Tests;

/// <summary>
/// Builds the out-of-solution fixture projects under <c>Tests/ManifestGenerator.Fixtures.*</c> with
/// the real <c>build/HackerOs.Apps.targets</c> wiring, like <c>RazorAssetValidationTests</c> does.
/// </summary>
public sealed class MsBuildIntegrationTests
{
    [Fact]
    public async Task Single_definition_project_generates_manifest_json()
    {
        (int exitCode, string output) = await BuildFixtureAsync("SingleApp");

        Assert.True(exitCode == 0, output);
        AppManifest manifest = AppManifestJsonSerializer.DeserializeStrict(
            await File.ReadAllTextAsync(Path.Combine(FixtureDirectory("SingleApp"), "manifest.json")));
        Assert.Equal("org.hackeros.fixtures.hello", manifest.Id);
        Assert.Equal("ManifestGenerator.Fixtures.SingleApp.dll", manifest.EntryPoint!.Assembly);
    }

    [Fact]
    public async Task Multi_definition_project_generates_one_file_per_app()
    {
        (int exitCode, string output) = await BuildFixtureAsync("MultiApp");

        Assert.True(exitCode == 0, output);
        string directory = Path.Combine(FixtureDirectory("MultiApp"), "manifests");
        Assert.Equal(
            ["org.hackeros.fixtures.alpha.json", "org.hackeros.fixtures.beta.json"],
            Directory.GetFiles(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.False(File.Exists(Path.Combine(FixtureDirectory("MultiApp"), "manifest.json")));
    }

    [Fact]
    public async Task Project_without_definition_fails_with_HOSAPP0001()
    {
        (int exitCode, string output) = await BuildFixtureAsync("NoDefinition");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("error HOSAPP0001", output, StringComparison.Ordinal);
        Assert.DoesNotContain("HOSAPP0099", output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(FixtureDirectory("NoDefinition"), "manifest.json")));
    }

    private static string FixtureDirectory(string name) =>
        Path.Combine(FindSolutionDirectory(), "Tests", $"ManifestGenerator.Fixtures.{name}");

    private static async Task<(int ExitCode, string Output)> BuildFixtureAsync(string name)
    {
        string solutionDirectory = FindSolutionDirectory();
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = solutionDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(Path.Combine(FixtureDirectory(name), $"ManifestGenerator.Fixtures.{name}.csproj"));
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("-p:NuGetAudit=false");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start the {name} fixture build.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string FindSolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HackerOs.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate HackerOs.sln.");
    }
}
