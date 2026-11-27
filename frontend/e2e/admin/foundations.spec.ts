// Acceptance Test
// Traces to: L2-088, L2-092, L2-093, L2-097, L2-098
// Description: The document carries the Keryx icon, self-hosted fonts and the design tokens in light and dark

import { expect, test } from '../fixtures/test';
import { DocumentPage } from '../page-objects/admin/document.page';

test.describe('Document foundations', () => {
  // L2-097 AC2: the tab shows the Keryx mark from favicon.ico, which holds seven sizes.
  // L2-092 AC7: the page language is set.
  test('links the Keryx icon with its seven sizes and sets the language', async ({ page }) => {
    const doc = new DocumentPage(page);

    await doc.open();

    expect(await doc.language()).toBe('en');
    expect(await doc.iconHref()).toBe('favicon.ico');
    expect(await doc.iconSizes()).toEqual([16, 24, 32, 48, 64, 128, 256]);
  });

  // L2-093 AC1 and L2-098 AC3: colours come from the tokens by role, and follow the system.
  // The canvas is ink 50 (#f4f5f7) in light and ink 975 (#0a0c10) in dark; text is ink 950
  // (#0e1116) in light and ink 50 in dark.
  test(
    'follows the system colour scheme through the design tokens',
    { tag: '@critical' },
    async ({ page }) => {
      const doc = new DocumentPage(page);

      await doc.prefer({ colorScheme: 'light' });
      await doc.open();
      expect(await doc.bodyStyle('background-color')).toBe('rgb(244, 245, 247)');
      expect(await doc.bodyStyle('color')).toBe('rgb(14, 17, 22)');

      await doc.prefer({ colorScheme: 'dark' });
      expect(await doc.bodyStyle('background-color')).toBe('rgb(10, 12, 16)');
      expect(await doc.bodyStyle('color')).toBe('rgb(244, 245, 247)');
    },
  );

  // L2-093 AC2: with reduced motion preferred the duration tokens are zero.
  test('stops motion when reduced motion is preferred', async ({ page }) => {
    const doc = new DocumentPage(page);

    await doc.prefer({ reducedMotion: 'no-preference' });
    await doc.open();
    expect(await doc.durationMs('--duration-fast')).toBe(150);

    await doc.prefer({ reducedMotion: 'reduce' });
    expect(await doc.durationMs('--duration-fast')).toBe(0);
    expect(await doc.durationMs('--duration-base')).toBe(0);
    expect(await doc.durationMs('--duration-slow')).toBe(0);
  });

  // L2-088 AC5: Geist comes from the app's own origin as woff2 with font-display: swap, and only
  // the face used on the screen is preloaded. Geist Mono is declared but nothing here uses it.
  test('serves Geist from its own origin and preloads only that face', async ({
    page,
    baseURL,
  }) => {
    const doc = new DocumentPage(page);

    await doc.open();

    const requested = doc.fontRequests();
    expect(requested).toHaveLength(1);
    expect(requested[0]).toMatch(/\/Geist-Variable\.woff2$/);
    expect(new URL(requested[0]).origin).toBe(new URL(baseURL ?? '').origin);
    expect(await doc.preloadedFonts()).toEqual(requested);
    expect(await doc.bodyStyle('font-family')).toMatch(/^"?Geist"?,/);
    expect(await doc.declaredFaces()).toEqual([
      { family: 'Geist', display: 'swap', status: 'loaded' },
      { family: 'Geist Mono', display: 'swap', status: 'unloaded' },
    ]);
  });
});
