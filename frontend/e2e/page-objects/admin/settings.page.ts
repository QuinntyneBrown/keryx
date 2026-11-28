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
  readonly saveBar: Locator;
  readonly saveBarText: Locator;
  readonly discard: Locator;
  readonly save: Locator;
  readonly summary: Locator;
  readonly summaryTitle: Locator;
  readonly summaryLinks: Locator;
  readonly saving: Locator;
  readonly viewRuns: Locator;
  readonly failure: Locator;
  readonly changed: Locator;
  readonly reload: Locator;

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
    this.saveBar = page.locator('main .save-bar');
    this.saveBarText = this.saveBar.locator('.save-bar__text');
    this.discard = this.saveBar.getByRole('button', { name: 'Discard' });
    this.save = this.saveBar.getByRole('button', { name: 'Save changes' });
    this.summary = page.locator('main .alert--danger');
    this.summaryTitle = this.summary.locator('.alert__title');
    this.summaryLinks = this.summary.locator('.alert__list a');
    this.saving = this.saveBar.getByRole('button', { name: 'Saving…' });
    this.viewRuns = this.saveBar.getByRole('link', { name: 'View runs' });
    this.failure = page.locator('main .alert--danger', { hasText: 'Settings weren’t saved' });
    this.changed = page.locator('main .alert--warning');
    this.reload = this.changed.getByRole('button', { name: 'Reload settings' });
  }

  async open(path = '/settings'): Promise<void> {
    await this.page.goto(path);
    await this.heading.waitFor();
  }

  /** The values of the chip list named `name` ("Topics", "Excluded terms", "Focus areas"). */
  chips(name: string): Locator {
    return this.page.getByRole('list', { name }).locator('.chip:not(.chip--add)');
  }

  /** The error a field shows under its control, which the control names as its description. */
  fieldError(control: Locator): Locator {
    return control
      .locator('xpath=ancestor::*[contains(@class, "field")][1]')
      .locator('.field__error');
  }

  /** The chips of a list marked new: added since the last save. */
  newChips(name: string): Locator {
    return this.page.getByRole('list', { name }).locator('.chip--new');
  }

  /** A chip's remove button, named "Remove <value>". */
  remove(value: string): Locator {
    return this.page.getByRole('button', { name: `Remove ${value}`, exact: true });
  }

  /** A list's Add chip, named "Add to <list>". */
  addTo(list: string): Locator {
    return this.page.getByRole('button', { name: `Add to ${list}`, exact: true });
  }

  /** A switch by its label. */
  switchNamed(name: string): Locator {
    return this.page.getByRole('switch', { name });
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
