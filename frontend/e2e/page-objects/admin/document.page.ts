import type { Page } from '@playwright/test';

/** One font face the document has declared. */
export interface DeclaredFace {
  family: string;
  display: string;
  status: string;
}

/**
 * The document around every screen: its language, icon, fonts and the global foundations
 * (design tokens read by role, light and dark). It owns no screen of its own.
 */
export class DocumentPage {
  private readonly fonts: string[] = [];

  constructor(private readonly page: Page) {
    page.on('request', (request) => {
      if (request.resourceType() === 'font') this.fonts.push(request.url());
    });
  }

  async open(path = '/'): Promise<void> {
    await this.page.goto(path);
    await this.page.locator('main').waitFor();
    await this.page.evaluate(() => document.fonts.ready);
  }

  /** Emulates the system preferences the tokens answer to. */
  async prefer(preferences: {
    colorScheme?: 'light' | 'dark';
    reducedMotion?: 'reduce' | 'no-preference';
  }): Promise<void> {
    await this.page.emulateMedia(preferences);
  }

  language(): Promise<string | null> {
    return this.page.locator('html').getAttribute('lang');
  }

  iconHref(): Promise<string | null> {
    return this.page.locator('link[rel="icon"]').getAttribute('href');
  }

  /** The sizes, in pixels, of the images inside the icon file the document links. */
  async iconSizes(): Promise<number[]> {
    const response = await this.page.request.get((await this.iconHref()) ?? '');
    if (!response.ok()) throw new Error(`The icon answered ${response.status()}`);
    const bytes = await response.body();
    // ICO: reserved (2 bytes), type 1 (2 bytes), count (2 bytes), then 16 bytes per image whose
    // first byte is the width, 0 standing for 256.
    if (bytes.readUInt16LE(0) !== 0 || bytes.readUInt16LE(2) !== 1) {
      throw new Error('The icon is not an ICO file');
    }
    const count = bytes.readUInt16LE(4);
    return Array.from({ length: count }, (_, index) => bytes.readUInt8(6 + index * 16) || 256);
  }

  /** A design token as the document resolves it now. */
  token(name: string): Promise<string> {
    return this.page.evaluate(
      (token) => getComputedStyle(document.documentElement).getPropertyValue(token).trim(),
      name,
    );
  }

  bodyStyle(property: string): Promise<string> {
    return this.page.evaluate(
      (name) => getComputedStyle(document.body).getPropertyValue(name),
      property,
    );
  }

  /** The font files the document fetched, as URLs. */
  fontRequests(): string[] {
    return [...this.fonts];
  }

  /** The font files the document preloads, as absolute URLs. */
  preloadedFonts(): Promise<string[]> {
    return this.page
      .locator('link[rel="preload"][as="font"]')
      .evaluateAll((links) => links.map((link) => (link as HTMLLinkElement).href));
  }

  declaredFaces(): Promise<DeclaredFace[]> {
    return this.page.evaluate(() =>
      [...document.fonts].map((face) => ({
        family: face.family.replace(/["']/g, ''),
        display: face.display,
        status: face.status,
      })),
    );
  }
}
