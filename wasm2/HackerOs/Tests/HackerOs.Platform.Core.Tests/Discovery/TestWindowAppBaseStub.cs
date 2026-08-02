namespace HackerOs.AppSdk.Blazor;

/// <summary>
/// Local stand-in for the real Blazor <c>WindowAppBase</c>, sharing its exact namespace and name
/// so <see cref="HackerOs.Platform.Core.Discovery.AppEntryPointDiscovery"/>'s name-based base-type
/// matching resolves it identically without this test project referencing the real Blazor
/// assembly (Platform Core itself never references it either; see remarks on
/// <see cref="HackerOs.Platform.Core.Discovery.AppEntryPointDiscovery"/>).
/// </summary>
internal abstract class WindowAppBase;
