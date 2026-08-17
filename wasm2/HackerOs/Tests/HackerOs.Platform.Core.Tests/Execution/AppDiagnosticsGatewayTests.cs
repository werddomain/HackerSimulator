using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Execution;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Tests.Execution;

public sealed class AppDiagnosticsGatewayTests
{
    [Fact]
    public void Clear_WithCapability_ClearsTheUnderlyingSink()
    {
        BoundedDiagnosticSink sink = new(maxEntries: 10);
        sink.Record(new DiagnosticEntry(
            DateTimeOffset.UnixEpoch, DiagnosticSeverity.Information, "test", "hello", Guid.NewGuid()));
        AppDiagnosticsGateway gateway = new(sink, new FakeCapabilityChecker(granted: true));

        gateway.Clear();

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void Clear_WithoutCapability_ThrowsAccessDenied()
    {
        BoundedDiagnosticSink sink = new(maxEntries: 10);
        sink.Record(new DiagnosticEntry(
            DateTimeOffset.UnixEpoch, DiagnosticSeverity.Information, "test", "hello", Guid.NewGuid()));
        AppDiagnosticsGateway gateway = new(sink, new FakeCapabilityChecker(granted: false));

        Assert.Throws<AppGatewayAccessDeniedException>(() => gateway.Clear());
        Assert.Single(sink.Entries);
    }

    private sealed class FakeCapabilityChecker(bool granted) : ICapabilityChecker
    {
        public CapabilityPolicyEvaluation Evaluate(
            string capability,
            AppAuthority requiredAuthority = AppAuthority.User,
            CapabilityResourceCandidate? resourceCandidate = null) =>
            throw new NotSupportedException("Evaluate is not exercised by AppDiagnosticsGateway.");

        public void Require(
            string capability,
            AppAuthority requiredAuthority = AppAuthority.User,
            CapabilityResourceCandidate? resourceCandidate = null)
        {
            if (!granted)
            {
                throw new AppGatewayAccessDeniedException(capability, CapabilityPolicyEvaluation.DenyMissing(1));
            }
        }
    }
}
