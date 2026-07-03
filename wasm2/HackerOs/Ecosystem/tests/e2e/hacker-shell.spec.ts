import { test, expect } from '@playwright/test';
import { boot, launchApp, taskbarButton } from './helpers';

test.describe('Hacker Shell terminal application', () => {
  test('launches with its banner and prompt', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.hackershell');

    await expect(taskbarButton(page, 'Hacker Shell')).toBeVisible();

    const screen = page.locator('.terminal-screen').first();
    await expect(screen).toContainText('HackerOS');
    // The prompt is written for the first input line.
    await expect(screen).toContainText('root@hackeros');
  });

  test('echoes typed input and executes commands', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.hackershell');

    const terminal = page.locator('.terminal-container').first();
    await expect(terminal).toBeVisible();
    await terminal.click({ force: true, noWaitAfter: true });

    // `echo` prints a contiguous token back that is easy to assert on.
    await page.keyboard.type('echo HELLOWORLD');
    await page.keyboard.press('Enter');

    const screen = page.locator('.terminal-screen').first();
    await expect(screen).toContainText('HELLOWORLD');

    // `whoami` is handled entirely in C#.
    await page.keyboard.type('whoami');
    await page.keyboard.press('Enter');
    await expect(screen).toContainText('root');
  });

  test('can launch other apps from the console', async ({ page }) => {
    await boot(page);
    await launchApp(page, 'hackeros.hackershell');

    const terminal = page.locator('.terminal-container').first();
    await expect(terminal).toBeVisible();
    await terminal.click({ force: true, noWaitAfter: true });

    await page.keyboard.type('launch hackeros.sysmon');
    await page.keyboard.press('Enter');

    await expect(page.getByTestId('sysmon-app')).toBeVisible();
    await expect(taskbarButton(page, 'System Monitor')).toBeVisible();
  });
});
