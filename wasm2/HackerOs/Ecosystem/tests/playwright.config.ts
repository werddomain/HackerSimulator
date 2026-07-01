import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for the HackerOS ecosystem end-to-end tests.
 *
 * The `webServer` block builds and serves the Blazor WebAssembly host so the
 * suite is fully self contained: `npm test` will compile and launch the app,
 * wait for it to respond, then drive it through a real browser. When a dev
 * server is already running on the port it is reused.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5229',
    trace: 'on-first-retry',
    // Blazor WASM needs a moment to download and start the runtime.
    actionTimeout: 15_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'dotnet run --project ../HackerOs.Ecosystem --urls http://localhost:5229',
    url: 'http://localhost:5229',
    reuseExistingServer: true,
    timeout: 240_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
