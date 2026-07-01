import { test, expect } from '@playwright/test';
import { boot, launchApp, taskbarButton } from './helpers';

test.describe('Welcome window application', () => {
  test('launches, self-populates its title bar and appears on the taskbar', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.welcome');

    // The window (a WindowAppBase) renders its content.
    await expect(page.getByTestId('welcome-app')).toBeVisible();
    await expect(page.locator('.welcome-title')).toContainText('Welcome to HackerOS');

    // The taskbar entry is created automatically by the window manager.
    await expect(taskbarButton(page, 'Welcome')).toBeVisible();
  });

  test('runs its collocated .razor.js module to report the runtime', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.welcome');

    // describeEnvironment() from WelcomeApp.razor.js fills this in.
    await expect(page.getByTestId('welcome-env')).toContainText('Blazor WebAssembly');
  });

  test('can launch another application from within the window', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.welcome');

    await page.getByTestId('welcome-open-monitor').click();
    await expect(page.getByTestId('sysmon-app')).toBeVisible();
    await expect(taskbarButton(page, 'System Monitor')).toBeVisible();
  });
});
