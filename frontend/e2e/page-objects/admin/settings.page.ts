import type { Locator, Page } from '@playwright/test';

/**
 * Settings (docs/mocks/pages/settings/): the settings form of journey 18 and the Account section
 * of journey 2.
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
  readonly body: Locator;
  readonly form: Locator;
  readonly sectionHeadings: Locator;
  readonly skeletonSections: Locator;
  readonly loadingStatus: Locator;
  readonly error: Locator;
  readonly errorTitle: Locator;
  readonly errorReference: Locator;
  readonly tryAgain: Locator;
  readonly runEvery: Locator;
  readonly runEveryOptions: Locator;
  readonly pressedOptions: Locator;
  readonly scheduleLine: Locator;
  readonly timeout: Locator;
  readonly suffixes: Locator;
  readonly threshold: Locator;
  readonly thresholdValue: Locator;
  readonly thresholdHelp: Locator;
  readonly switches: Locator;
  readonly retention: Locator;
  readonly alertEmail: Locator;
  readonly alertStreak: Locator;

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
    this.body = page.locator('main .page-body');
    this.form = page.locator('main form.page-body');
    this.sectionHeadings = page.locator('main .settings-section h2');
    this.skeletonSections = this.body.locator('.settings-section:has(.skeleton)');
    this.loadingStatus = this.body.locator('[role="status"]');
    this.error = page.locator('main .empty--error');
    this.errorTitle = this.error.locator('.empty__title');
    this.errorReference = this.error.locator('.empty__ref');
    this.tryAgain = this.error.getByRole('button', { name: 'Try again' });
    this.runEvery = page.getByRole('group', { name: 'Run every' });
    this.runEveryOptions = this.runEvery.locator('.segmented__option');
    this.pressedOptions = this.runEvery.locator('.segmented__option[aria-pressed="true"]');
    this.scheduleLine = this.runEvery.locator('.mono');
    this.timeout = page.getByLabel('Timeout per source');
    this.suffixes = page.locator('main .input-affix__suffix');
    this.threshold = page.getByRole('slider', { name: 'Publish automatically above' });
    this.thresholdValue = page.locator('main .range__value');
    this.thresholdHelp = page.locator('#threshold-help');
    this.switches = page.getByRole('switch');
    this.retention = page.getByLabel('Keep articles for');
    this.alertEmail = page.getByLabel('E-mail');
    this.alertStreak = page.getByLabel('Alert when a source fails');
  }

  async open(path = '/settings'): Promise<void> {
    await this.page.goto(path);
    await this.heading.waitFor();
  }

  /** The values of the chip list named `name` ("Topics", "Excluded terms", "Focus areas"). */
  chips(name: string): Locator {
    return this.page.getByRole('list', { name }).locator('.chip:not(.chip--add)');
  }

  /** A "Run every" option by its label. */
  option(label: string): Locator {
    return this.runEvery.getByRole('button', { name: label, exact: true });
  }

  /** How far a switch's thumb is moved: its position, not its colour, shows the state. */
  thumbShift(control: Locator): Promise<string> {
    return control.evaluate((element) => getComputedStyle(element, '::before').translate);
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
