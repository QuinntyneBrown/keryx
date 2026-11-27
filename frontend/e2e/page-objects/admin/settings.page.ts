import type { Locator, Page } from '@playwright/test';

/**
 * Settings (docs/mocks/pages/settings/default.html). Journey 2 builds its Account section only;
 * the settings form arrives with journey 18 (ADR 0006).
 */
export class SettingsPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly navigation: Locator;
  readonly navLink: Locator;
  readonly currentLinks: Locator;
  readonly account: Locator;
  readonly accountHeading: Locator;
  readonly signedInLabel: Locator;
  readonly signedInAs: Locator;
  readonly signOut: Locator;
  readonly signOutFailure: Locator;

  constructor(private readonly page: Page) {
    const header = page.locator('main header.page-header');
    this.heading = header.locator('h1');
    this.description = header.locator('.page-header__description');
    this.navigation = page.getByRole('navigation', { name: 'Primary' });
    this.navLink = this.navigation.locator('a.nav-link', { hasText: 'Settings' });
    this.currentLinks = this.navigation.locator('a.nav-link[aria-current="page"]');
    this.account = page.getByRole('region', { name: 'Account' });
    this.accountHeading = this.account.locator('h2');
    this.signedInLabel = this.account.locator('.field__label');
    this.signedInAs = this.account.locator('.field p');
    this.signOut = this.account.getByRole('button', { name: 'Sign out' });
    this.signOutFailure = this.account.locator('.alert');
  }

  async open(path = '/settings'): Promise<void> {
    await this.page.goto(path);
    await this.heading.waitFor();
  }

  path(): string {
    return new URL(this.page.url()).pathname;
  }

  search(): URLSearchParams {
    return new URL(this.page.url()).searchParams;
  }

  /** A computed style of the first element `locator` matches. */
  styleOf(locator: Locator, property: string): Promise<string> {
    return locator
      .first()
      .evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);
  }
}
