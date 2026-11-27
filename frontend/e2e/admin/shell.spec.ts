// Acceptance Test
// Traces to: L2-088, L2-091, L2-092, L2-097, L2-098
// Description: The admin app opens on Overview inside the shell, built only from design-system components

import { expect, test } from '../fixtures/test';
import { ShellPage } from '../page-objects/admin/shell.page';

// Overview's description: text only its own lazy chunk holds.
const OVERVIEW_TEXT = 'Christian fitness news collected across Toronto & the GTA.';

test.describe('Admin shell', () => {
  // L2-097 AC3, L2-098 AC1 and AC4, L2-091 AC3, L2-092 AC7.
  test(
    'opens on the frame with the lockup as the home link and one heading',
    { tag: '@critical' },
    async ({ page }) => {
      const shell = new ShellPage(page);

      await shell.open();

      await expect.poll(() => shell.title()).toBe('Overview · Keryx');
      await expect(shell.frame).toHaveCount(1);
      await expect(shell.main).toHaveCount(1);
      await expect(shell.main).toHaveAttribute('id', 'main');
      await expect(shell.main).toHaveClass(/(^|\s)app__main(\s|$)/);
      await expect(shell.brand).toHaveAccessibleName('Keryx, go to overview');
      await expect(shell.wordmark).toHaveText('keryx');
      await expect(shell.tag).toHaveText('Admin');
      await expect(shell.headings).toHaveCount(1);
      await expect(shell.headings).toHaveText('Overview');
      // ADR 0006: each screen's link arrives with the screen; Runs comes with journey 14.
      await expect(shell.navigation).toHaveCount(1);
      await expect(shell.navigation).toHaveAccessibleName('Primary');
      await expect(shell.navigation.getByRole('link')).toHaveText([
        'Overview',
        'Articles',
        'Sources',
        'API',
        'Settings',
      ]);
    },
  );

  // L2-097 AC3 and AC4: the 40-unit drawing, four square-ended strokes of width 4.2 in volt on an
  // ink tile of radius 10. Volt is #c6f135 and the sidebar's tile is ink 2, #181c23.
  test('draws the mark as the design system defines it', async ({ page }) => {
    const shell = new ShellPage(page);

    await shell.open();

    await expect(shell.mark).toHaveAttribute('viewBox', '0 0 40 40');
    await expect(shell.mark).toHaveAttribute('aria-hidden', 'true');
    await expect(shell.markTile).toHaveAttribute('width', '40');
    await expect(shell.markTile).toHaveAttribute('height', '40');
    await expect(shell.markTile).toHaveAttribute('rx', '10');
    await expect(shell.markStrokes).toHaveCount(4);
    expect(
      await shell.markStrokes.evaluateAll((paths) => paths.map((p) => p.getAttribute('d'))),
    ).toEqual(['M15 9V31', 'M9 16H15', 'M15 16L28.5 9', 'M20 20L29 31']);
    expect(await shell.styleOf(shell.markStrokes, 'stroke-width')).toBe('4.2px');
    expect(await shell.styleOf(shell.markStrokes, 'stroke-linecap')).toBe('square');
    expect(await shell.styleOf(shell.markStrokes, 'stroke')).toBe('rgb(198, 241, 53)');
    expect(await shell.styleOf(shell.markStrokes, 'fill')).toBe('none');
    expect(await shell.styleOf(shell.markTile, 'fill')).toBe('rgb(24, 28, 35)');
    // Never stretched or skewed: the mark is as wide as it is tall.
    const box = await shell.mark.boundingBox();
    expect(box?.width).toBe(28);
    expect(box?.height).toBe(28);
  });

  // L2-088 AC2: the shell is its own lazy chunk and is not in the initial bundle.
  test('loads the shell as a lazy chunk, outside the initial bundle', async ({ page }) => {
    const shell = new ShellPage(page);

    await shell.open();
    await expect(shell.description).toHaveText(OVERVIEW_TEXT);

    const holding = await shell.scriptsHolding('Christian fitness news collected across Toronto');
    const initial = await shell.initialScripts();
    expect(holding).toHaveLength(1);
    expect(initial.length).toBeGreaterThan(0);
    expect(initial).not.toContain(holding[0]);
  });

  // L2-091 AC1: the skip link is the first focusable element and moves focus to main.
  test('skips to the content from the first Tab', async ({ page }) => {
    const shell = new ShellPage(page);
    await shell.open();

    await shell.pressTab();
    await expect(shell.skipLink).toBeFocused();
    await expect(shell.skipLink).toBeInViewport();
    await shell.pressEnter();

    await expect(shell.main).toBeFocused();
    expect(shell.path()).toBe('/');
  });

  // L2-097 AC3: the lockup is a link to Overview; following it stays inside the app.
  test('follows the lockup without loading the document again', async ({ page }) => {
    const shell = new ShellPage(page);
    await shell.open();
    await shell.markDocument();

    await shell.brand.click();

    await expect(shell.headings).toHaveText('Overview');
    expect(shell.path()).toBe('/');
    expect(await shell.documentWasKept()).toBe(true);
  });
});

test.describe('Signed-in person', () => {
  // L2-039 AC5, L2-042 AC1 and AC7: the shell shows who is signed in, read from GET /admin/v1/me.
  test('shows the initials, name and role in the sidebar footer', async ({ page, api }) => {
    api.on('GET', '/admin/v1/me', () => ({
      json: { name: 'Grace Okafor', initials: 'GO', role: 'Editor' },
    }));
    const shell = new ShellPage(page);

    await shell.open();

    await expect(shell.footer).toBeVisible();
    await expect(shell.avatar).toHaveText('GO');
    await expect(shell.avatar).toHaveAttribute('aria-hidden', 'true');
    await expect(shell.userName).toHaveText('Grace Okafor');
    await expect(shell.userRole).toHaveText('Editor');
  });

  // L2-042 AC7: below 1024 px the footer, and the person in it, is not shown.
  test('hides the person with the footer below 1024 px', async ({ page }) => {
    const shell = new ShellPage(page);
    await shell.resize(1023);

    await shell.open();

    await expect(shell.userName).toBeHidden();
    await shell.resize(1024);
    await expect(shell.userName).toHaveText('Daniel Adeyemi');
  });
});
