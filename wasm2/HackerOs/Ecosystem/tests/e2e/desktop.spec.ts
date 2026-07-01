import { test, expect } from '@playwright/test';
import { boot, openStartMenu } from './helpers';

test.describe('Desktop shell and self-registration', () => {
  test('desktop boots and the taskbar start button is present', async ({ page }) => {
    await boot(page);
    await expect(page.getByTestId('start-button')).toContainText('START');
  });

  test('the START menu lists every self-registered application', async ({ page }) => {
    await boot(page);
    await openStartMenu(page);

    // Each [App]-decorated module must appear without any manual wiring.
    await expect(
      page.locator('[data-testid="app-item"][data-app-id="hackeros.welcome"]'),
    ).toBeVisible();
    await expect(
      page.locator('[data-testid="app-item"][data-app-id="hackeros.sysmon"]'),
    ).toBeVisible();
    await expect(
      page.locator('[data-testid="app-item"][data-app-id="hackeros.hackershell"]'),
    ).toBeVisible();

    const count = await page.getByTestId('app-item').count();
    expect(count).toBeGreaterThanOrEqual(3);
  });

  test('the START menu closes when clicking outside of it', async ({ page }) => {
    await boot(page);
    await openStartMenu(page);
    await page.getByTestId('desktop').click({ position: { x: 400, y: 200 } });
    await expect(page.getByTestId('app-menu')).toBeHidden();
  });
});
