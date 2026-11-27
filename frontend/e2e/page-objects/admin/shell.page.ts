import type { Locator, Page, Response } from '@playwright/test';

/**
 * The admin shell: the frame every screen sits in (docs/design-system/components/app.html,
 * sidebar.html, brand.html, skip-link.html). Until journey 2 it is the placeholder of ADR 0004:
 * the frame, the lockup and one empty-state card. The locators are the design system's BEM
 * classes, which the mocks and the components library share.
 */
export class ShellPage {
  readonly skipLink: Locator;
  readonly frame: Locator;
  readonly main: Locator;
  readonly sidebar: Locator;
  /** The lockup: the mark and the wordmark as one home link. */
  readonly brand: Locator;
  readonly mark: Locator;
  readonly markTile: Locator;
  readonly markStrokes: Locator;
  readonly wordmark: Locator;
  readonly tag: Locator;
  readonly navigation: Locator;
  readonly headings: Locator;
  readonly placeholder: Locator;
  readonly placeholderText: Locator;
  /** The card's icon disc and action row, which take no room while nothing is in them. */
  readonly placeholderIcon: Locator;
  readonly placeholderActions: Locator;
  /** The sidebar footer and the signed-in person in it. */
  readonly footer: Locator;
  readonly avatar: Locator;
  readonly userName: Locator;
  readonly userRole: Locator;

  private readonly scripts: Promise<{ path: string; text: string }>[] = [];

  constructor(private readonly page: Page) {
    this.skipLink = page.locator('.skip-link');
    this.frame = page.locator('.app');
    this.main = page.locator('main');
    this.sidebar = page.locator('aside.sidebar');
    this.brand = this.sidebar.locator('.sidebar__head a.brand');
    this.mark = this.brand.locator('svg.brand__mark');
    this.markTile = this.mark.locator('rect.brand__tile');
    this.markStrokes = this.mark.locator('.brand__strokes path');
    this.wordmark = this.brand.locator('.brand__word');
    this.tag = this.sidebar.locator('.sidebar__tag');
    this.navigation = page.getByRole('navigation');
    this.headings = page.locator('h1');
    this.placeholder = this.main.locator('.empty');
    this.placeholderText = this.placeholder.locator('.empty__text');
    this.placeholderIcon = this.placeholder.locator('.empty__icon');
    this.placeholderActions = this.placeholder.locator('.empty__actions');
    this.footer = this.sidebar.locator('.sidebar__foot');
    this.avatar = this.footer.locator('.sidebar__user .avatar');
    this.userName = this.footer.locator('.sidebar__user-name');
    this.userRole = this.footer.locator('.sidebar__user .sidebar__caption');

    page.on('response', (response) => this.keepScript(response));
  }

  /** Opens the app at `path` and waits for the shell to render. */
  async open(path = '/'): Promise<void> {
    await this.page.goto(path);
    await this.frame.waitFor();
  }

  /** Sets the viewport width, keeping the height. */
  async resize(width: number): Promise<void> {
    await this.page.setViewportSize({ width, height: 800 });
  }

  title(): Promise<string> {
    return this.page.title();
  }

  path(): string {
    return new URL(this.page.url()).pathname;
  }

  /**
   * The paths of the scripts in the initial bundle: the one the document starts from and every
   * script reached from it by a static import. A script loaded by `import()` is not among them.
   */
  async initialScripts(): Promise<string[]> {
    const scripts = new Map((await Promise.all(this.scripts)).map((s) => [s.path, s.text]));
    const src = await this.page
      .locator('script[type="module"][src*="main"]')
      .first()
      .getAttribute('src');
    const initial = new Set<string>();
    const pending = [new URL(src ?? '', this.page.url()).pathname];
    for (let path = pending.pop(); path !== undefined; path = pending.pop()) {
      const text = scripts.get(path);
      if (text === undefined || initial.has(path)) continue;
      initial.add(path);
      // `import … from "x"` and `import "x"`, minified or not; never the call `import("x")`.
      for (const [, specifier] of text.matchAll(/(?:\bfrom|\bimport)\s*["']([^"']+)["']/g)) {
        pending.push(new URL(specifier, new URL(path, this.page.url())).pathname);
      }
    }
    return [...initial];
  }

  /** The paths of the loaded scripts whose code holds `text`. */
  async scriptsHolding(text: string): Promise<string[]> {
    const scripts = await Promise.all(this.scripts);
    return scripts.filter((script) => script.text.includes(text)).map((script) => script.path);
  }

  /** Marks the document, so a later check can tell whether it was loaded again. */
  async markDocument(): Promise<void> {
    await this.page.evaluate(() => document.documentElement.setAttribute('data-e2e-kept', ''));
  }

  documentWasKept(): Promise<boolean> {
    return this.page.evaluate(() => document.documentElement.hasAttribute('data-e2e-kept'));
  }

  async pressTab(): Promise<void> {
    await this.page.keyboard.press('Tab');
  }

  async pressEnter(): Promise<void> {
    await this.page.keyboard.press('Enter');
  }

  /** A computed style of the first element `locator` matches. */
  styleOf(locator: Locator, property: string): Promise<string> {
    return locator
      .first()
      .evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);
  }

  private keepScript(response: Response): void {
    if (response.request().resourceType() !== 'script') return;
    const path = new URL(response.url()).pathname;
    this.scripts.push(
      response.text().then(
        (text) => ({ path, text }),
        () => ({ path, text: '' }),
      ),
    );
  }
}
