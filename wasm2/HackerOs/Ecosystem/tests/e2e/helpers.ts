import { Page, expect } from '@playwright/test';

/**
 * Navigates to the desktop and waits for the Blazor WASM runtime to boot.
 */
export async function boot(page: Page): Promise<void> {
  await page.goto('/');
  // The desktop shell renders once the framework has started.
  await expect(page.getByTestId('desktop')).toBeVisible({ timeout: 45_000 });
  // Wait for the start button to be interactive.
  await expect(page.getByTestId('start-button')).toBeVisible();
}

/**
 * Opens the START menu.
 */
export async function openStartMenu(page: Page): Promise<void> {
  await page.getByTestId('start-button').click();
  await expect(page.getByTestId('app-menu')).toBeVisible();
}

/**
 * Launches an application from the START menu by its registered id.
 */
export async function launchApp(page: Page, appId: string): Promise<void> {
  await openStartMenu(page);
  await page.locator(`[data-testid="app-item"][data-app-id="${appId}"]`).click({ noWaitAfter: true });
  // Menu closes after launching.
  await expect(page.getByTestId('app-menu')).toBeHidden();
}

/**
 * Returns the taskbar button locator for a window whose title matches.
 */
export function taskbarButton(page: Page, title: string) {
  return page.locator('.window-button', { hasText: title });
}
