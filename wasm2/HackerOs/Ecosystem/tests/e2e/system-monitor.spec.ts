import { test, expect } from '@playwright/test';
import { boot, launchApp, taskbarButton } from './helpers';

test.describe('System Monitor window application', () => {
  test('launches and renders its resource gauges', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.sysmon');

    await expect(page.getByTestId('sysmon-app')).toBeVisible();
    await expect(page.getByTestId('sysmon-cpu')).toBeVisible();
    await expect(page.getByTestId('sysmon-mem')).toBeVisible();
    await expect(page.getByTestId('sysmon-net')).toBeVisible();
    await expect(taskbarButton(page, 'System Monitor')).toBeVisible();
  });

  test('polls its collocated sampler so samples increase over time', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.sysmon');

    const ticks = page.getByTestId('sysmon-ticks');
    await expect(ticks).toContainText('samples:');

    const readCount = async () =>
      Number((await ticks.textContent())?.replace(/\D/g, '') ?? '0');

    const initial = await readCount();
    // The periodic timer samples once per second.
    await page.waitForTimeout(2500);
    const later = await readCount();
    expect(later).toBeGreaterThan(initial);
  });
});
