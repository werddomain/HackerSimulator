import { test, expect } from '@playwright/test';
import { boot, launchApp, taskbarButton } from './helpers';

test.describe('Taskbar window management', () => {
  test('minimizes and restores a window from its taskbar button', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.welcome');

    await expect(page.getByTestId('welcome-app')).toBeVisible();
    const button = taskbarButton(page, 'Welcome');
    await expect(button).toBeVisible();

    // Clicking the active window's taskbar button minimizes it.
    await button.click();
    await expect(page.getByTestId('welcome-app')).toBeHidden();

    // Clicking again restores it.
    await button.click();
    await expect(page.getByTestId('welcome-app')).toBeVisible();
  });

  test('tracks multiple windows independently on the taskbar', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.welcome');
    await launchApp(page, 'hackeros.sysmon');

    await expect(taskbarButton(page, 'Welcome')).toBeVisible();
    await expect(taskbarButton(page, 'System Monitor')).toBeVisible();
    await expect(page.locator('.window-button')).toHaveCount(2);
  });
});
