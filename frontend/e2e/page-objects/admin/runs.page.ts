import type { Locator, Page } from '@playwright/test';

/** Runs (docs/mocks/pages/runs/): the run history beside the chosen run's detail. */
export class RunsPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly navLink: Locator;
  readonly overviewLink: Locator;
  readonly history: Locator;
  readonly runs: Locator;
  readonly runTitles: Locator;
  readonly runMetas: Locator;
  readonly runWords: Locator;
  readonly runFlags: Locator;
  /** The spinner of a run in progress, in place of its outcome's icon. */
  readonly runSpinners: Locator;
  readonly current: Locator;
  readonly detail: Locator;
  readonly detailHeading: Locator;
  readonly detailBadge: Locator;
  readonly detailMeta: Locator;
  readonly tileLabels: Locator;
  readonly tileValues: Locator;
  readonly pipelineHeading: Locator;
  readonly steps: Locator;
  readonly stepNames: Locator;
  readonly stepWords: Locator;
  readonly stepTimes: Locator;
  readonly stepDetails: Locator;
  readonly issuesHeading: Locator;
  readonly issues: Locator;
  readonly issueTitles: Locator;
  readonly issueWords: Locator;
  readonly issueSubjects: Locator;
  readonly issueDetails: Locator;
  readonly noIssues: Locator;
  readonly filter: Locator;
  readonly allRuns: Locator;
  readonly withProblems: Locator;
  /** The history's state when no run matches, and the detail's when no run is chosen. */
  readonly noResults: Locator;
  readonly chooseRun: Locator;
  /** The whole-page empty or error state. */
  readonly pageState: Locator;
  readonly pageStateTitle: Locator;
  readonly pageStateText: Locator;
  readonly pageStateReference: Locator;
  readonly pageStateActions: Locator;
  /** The detail's own error, while the list stays usable. */
  readonly detailError: Locator;
  readonly body: Locator;
  /** Below 1024 px, the way from a run back to the list (`pages/runs/narrow-detail.html`). */
  readonly back: Locator;
  readonly tiles: Locator;
  readonly loadingStatus: Locator;
  /** Skeleton rows standing in for the history, and the card and stages for the detail. */
  readonly runSkeletons: Locator;
  readonly cardSkeleton: Locator;
  readonly stepSkeletons: Locator;

  /** The header's "Run now", and the notifications the shell shows (newest last). */
  readonly runNow: Locator;
  readonly toast: Locator;
  readonly toastTitle: Locator;
  readonly toastText: Locator;
  readonly toastAction: Locator;

  constructor(private readonly page: Page) {
    const main = page.locator('main');
    this.heading = main.getByRole('heading', { level: 1 });
    this.runNow = page.locator('main .page-header__actions .btn');
    this.toast = page.locator('.toast-region .toast');
    this.toastTitle = this.toast.locator('.toast__title');
    this.toastText = this.toast.locator('.toast__text');
    this.toastAction = this.toast.locator('.toast__action');
    this.description = main.locator('.page-header__description');
    this.navLink = page.getByRole('link', { name: 'Runs', exact: true });
    this.overviewLink = page.getByRole('link', { name: 'Overview', exact: true });
    this.history = main.getByRole('region', { name: 'Run history' });
    this.runs = this.history.locator('a.run-item');
    this.runTitles = this.runs.locator('.list__title');
    this.runMetas = this.runs.locator('.list__meta');
    this.runWords = this.runs.locator('.visually-hidden');
    this.runFlags = this.runs.locator('.run-item__flag');
    this.runSpinners = this.runs.locator('.spinner');
    this.current = this.runs.and(page.locator('[aria-current="true"]'));
    this.detail = main.locator('section[aria-labelledby="run-title"]');
    this.detailHeading = this.detail.locator('#run-title');
    this.detailBadge = this.detail.locator('.card__title-row .badge');
    this.detailMeta = this.detail.locator('.card__meta');
    this.tileLabels = this.detail.locator('.stats .stat__label');
    this.tileValues = this.detail.locator('.stats .stat__value');
    this.pipelineHeading = this.detail.locator('#pipeline-title');
    this.steps = this.detail.locator('ol.timeline > li.timeline__step');
    this.stepNames = this.steps.locator('.timeline__name');
    this.stepWords = this.stepNames.locator('.visually-hidden');
    this.stepTimes = this.steps.locator('.timeline__time');
    this.stepDetails = this.steps.locator('.timeline__detail');
    const issues = this.detail.locator('section[aria-labelledby="issues-title"]');
    this.issuesHeading = issues.locator('#issues-title');
    this.issues = issues.locator('.list > li.list__item');
    this.issueTitles = this.issues.locator('.list__title');
    this.issueWords = this.issueTitles.locator('.visually-hidden');
    this.issueSubjects = this.issueTitles.locator('.text-muted');
    this.issueDetails = this.issues.locator('.issue__detail');
    this.noIssues = issues.locator('.empty');
    this.filter = main.getByRole('group', { name: 'Filter runs' });
    this.allRuns = this.filter.getByRole('link', { name: 'All runs' });
    this.withProblems = this.filter.getByRole('link', { name: 'With problems' });
    this.noResults = this.history.locator('.empty');
    this.chooseRun = main.locator('.empty').filter({ hasText: 'Choose a run' });
    this.pageState = main.locator('.page-body > kx-empty > .empty--card');
    this.pageStateTitle = this.pageState.locator('.empty__title');
    this.pageStateText = this.pageState.locator('.empty__text');
    this.pageStateReference = this.pageState.locator('.empty__ref');
    this.pageStateActions = this.pageState.locator('.empty__actions .btn');
    this.detailError = main.locator('section[aria-labelledby="run-title"] .alert');
    this.body = main.locator('.page-body');
    this.back = main.getByRole('link', { name: 'Back to runs' });
    this.tiles = this.detail.locator('.stats .stat');
    this.loadingStatus = this.body.getByRole('status');
    this.runSkeletons = this.history.locator('li.list__item.run-item:has(.skeleton)');
    this.cardSkeleton = main.locator('.card:has(.skeleton)');
    this.stepSkeletons = main.locator('.timeline__step:has(.skeleton)');
  }

  /** Each run's icon tone, in list order. */
  async runTones(): Promise<(string | null)[]> {
    return this.runs.evaluateAll((items) =>
      items.map((item) => item.querySelector('[data-tone]')?.getAttribute('data-tone') ?? null),
    );
  }

  /** The page's address after the path: its query string. */
  search(): string {
    return new URL(this.page.url()).search;
  }

  /** Each stage's `data-state`, in order. */
  async stepStates(): Promise<(string | null)[]> {
    return this.steps.evaluateAll((steps) => steps.map((step) => step.getAttribute('data-state')));
  }

  /** Each issue's streak, or null where it has none, in order. */
  async issueStreaks(): Promise<(string | null)[]> {
    return this.issues.evaluateAll((items) =>
      items.map((item) => item.querySelector('.issue__streak')?.textContent ?? null),
    );
  }

  /** Each issue's icon tone, in order. */
  async issueTones(): Promise<(string | null)[]> {
    return this.issues.evaluateAll((items) =>
      items.map((item) => item.querySelector('[data-tone]')?.getAttribute('data-tone') ?? null),
    );
  }

  async open(query = ''): Promise<void> {
    await this.page.goto(`/runs${query}`);
  }

  /** Whether the page scrolls sideways, which no width may make it do. */
  scrollsSideways(): Promise<boolean> {
    return this.page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
  }

  /** A row's spinner and flag, by the row's place in the list. */
  rowSpinner(index: number): Locator {
    return this.runs.nth(index).locator('.spinner');
  }

  rowFlag(index: number): Locator {
    return this.runs.nth(index).locator('.run-item__flag');
  }

  /**
   * How the running row's spinner looks beside the icons of other rows: its width against a
   * finished row's icon, and its colour against the info icon colour.
   */
  spinnerLook(): Promise<{ width: number; iconWidth: number; color: string; info: string }> {
    return this.history.evaluate((history) => {
      const spinner = history.querySelector<HTMLElement>('.run-item .spinner');
      const icon = history.querySelector<HTMLElement>(
        '.run-item__icon:not([data-tone="info"]) svg',
      );
      const probe = document.createElement('span');
      probe.style.color = 'var(--color-info-icon)';
      history.append(probe);
      const info = getComputedStyle(probe).color;
      probe.remove();
      return {
        // Its layout width: it turns, so its bounding box grows as it does.
        width: spinner?.offsetWidth ?? 0,
        iconWidth: icon?.getBoundingClientRect().width ?? -1,
        color: spinner ? getComputedStyle(spinner).borderTopColor : '',
        info,
      };
    });
  }
}
