# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: desktop.spec.ts >> Desktop shell and self-registration >> desktop boots and the taskbar start button is present
- Location: e2e/desktop.spec.ts:5:7

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByTestId('desktop')
Expected: visible
Timeout: 45000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" with timeout 45000ms
  - waiting for getByTestId('desktop')

```

```yaml
- img
- text: 96% An unhandled error has occurred.
- link "Reload":
  - /url: .
- text: 🗙
```

# Test source

```ts
  1  | import { Page, expect } from '@playwright/test';
  2  | 
  3  | /**
  4  |  * Navigates to the desktop and waits for the Blazor WASM runtime to boot.
  5  |  */
  6  | export async function boot(page: Page): Promise<void> {
  7  |   await page.goto('/');
  8  |   // The desktop shell renders once the framework has started.
> 9  |   await expect(page.getByTestId('desktop')).toBeVisible({ timeout: 45_000 });
     |                                             ^ Error: expect(locator).toBeVisible() failed
  10 |   // Wait for the start button to be interactive.
  11 |   await expect(page.getByTestId('start-button')).toBeVisible();
  12 | }
  13 | 
  14 | /**
  15 |  * Opens the START menu.
  16 |  */
  17 | export async function openStartMenu(page: Page): Promise<void> {
  18 |   await page.getByTestId('start-button').click();
  19 |   await expect(page.getByTestId('app-menu')).toBeVisible();
  20 | }
  21 | 
  22 | /**
  23 |  * Launches an application from the START menu by its registered id.
  24 |  */
  25 | export async function launchApp(page: Page, appId: string): Promise<void> {
  26 |   await openStartMenu(page);
  27 |   await page.locator(`[data-testid="app-item"][data-app-id="${appId}"]`).click({ noWaitAfter: true });
  28 |   // Menu closes after launching.
  29 |   await expect(page.getByTestId('app-menu')).toBeHidden();
  30 | }
  31 | 
  32 | /**
  33 |  * Returns the taskbar button locator for a window whose title matches.
  34 |  */
  35 | export function taskbarButton(page: Page, title: string) {
  36 |   return page.locator('.window-button', { hasText: title });
  37 | }
  38 | 
```