# HackerOs.Samples.ServiceApp

Sample session service application for HackerOS demonstrating non-visual background service execution,
event publishing, session cancellation handling, and bounded cleanup.

## Features

- Derives `ServiceAppBase` with `AutoStart: true` manifest configuration.
- Runs a deterministic ticker loop using `IAppClockGateway.DelayAsync`.
- Publishes `SampleTickerEvent` to `IAppEventGateway` on every tick.
- Handles `CancellationToken` for graceful stop during logout, disable, or system shutdown.
- Performs bounded cleanup in `OnStoppingAsync` with no volatile state leak across restarts.
