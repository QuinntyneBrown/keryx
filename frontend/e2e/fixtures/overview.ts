/** 28 November 2026, 9:46 AM in Toronto; the 6-hourly run at 6:00 AM was the last. */
export const OVERVIEW_NOW = new Date('2026-11-28T14:46:00Z');

/** A collection run as Overview's status serves it; the mock's run_2418 by default. */
export function overviewRun(fields: Record<string, unknown> = {}) {
  return {
    id: 'run_2418',
    started_at: '2026-11-28T11:00:00Z',
    finished_at: '2026-11-28T11:04:12Z',
    duration_ms: 252_000,
    outcome: 'completed_with_issues',
    failure_code: null,
    added: 47,
    sources_ok: 62,
    sources_checked: 64,
    sources_total: 64,
    ...fields,
  };
}

/** Overview's status: the last run of the mocks, none in progress, and the next run at 12:00 PM. */
export function overviewStatus(fields: Record<string, unknown> = {}) {
  return {
    last_run: overviewRun(),
    current_run: null,
    has_sources: true,
    has_runs: true,
    next_run_at: '2026-11-28T17:00:00Z',
    time_zone: 'America/Toronto',
    ...fields,
  };
}

/** A run as the runs list serves it (View run history design). */
export function runSummary(
  id: string,
  started_at: string,
  outcome: string,
  added: number,
): Record<string, unknown> {
  return {
    id,
    trigger: 'scheduled',
    started_at,
    finished_at: outcome === 'running' ? null : started_at,
    duration_ms: outcome === 'running' ? null : 240_000,
    outcome,
    added,
    sources_failed: outcome === 'completed_with_issues' ? 2 : 0,
  };
}

/**
 * The mock's last 14 runs, newest first as the API serves them: every six hours from Wednesday
 * 6:00 AM to today 6:00 AM in Toronto, a failed run yesterday at noon and a manual one at 2:37 PM.
 */
export const LAST_14_RUNS = [
  runSummary('run_2418', '2026-11-28T11:00:00Z', 'completed_with_issues', 47),
  runSummary('run_2417', '2026-11-28T05:00:00Z', 'succeeded', 12),
  runSummary('run_2416', '2026-11-27T23:00:00Z', 'succeeded', 31),
  runSummary('run_2415', '2026-11-27T19:37:00Z', 'succeeded', 8),
  runSummary('run_2414', '2026-11-27T17:00:00Z', 'failed', 0),
  runSummary('run_2413', '2026-11-27T11:00:00Z', 'succeeded', 39),
  runSummary('run_2412', '2026-11-27T05:00:00Z', 'succeeded', 6),
  runSummary('run_2411', '2026-11-26T23:00:00Z', 'succeeded', 28),
  runSummary('run_2410', '2026-11-26T17:00:00Z', 'completed_with_issues', 22),
  runSummary('run_2409', '2026-11-26T11:00:00Z', 'succeeded', 41),
  runSummary('run_2408', '2026-11-26T05:00:00Z', 'succeeded', 9),
  runSummary('run_2407', '2026-11-25T23:00:00Z', 'succeeded', 35),
  runSummary('run_2406', '2026-11-25T17:00:00Z', 'succeeded', 26),
  runSummary('run_2405', '2026-11-25T11:00:00Z', 'succeeded', 19),
];

/** A page of the runs list. */
export function runPage(items: readonly Record<string, unknown>[], total = items.length) {
  return { items, total, page: 1, page_size: 14 };
}

/** The regions the latest articles are named with. */
export const OVERVIEW_REGIONS = [
  { slug: 'toronto', name: 'Toronto', time_zone: 'America/Toronto' },
  { slug: 'mississauga', name: 'Mississauga', time_zone: 'America/Toronto' },
  { slug: 'vaughan', name: 'Vaughan', time_zone: 'America/Toronto' },
];

const HOUR = 60 * 60 * 1000;

/** An article row collected `hoursAgo` before Overview's now. */
function latest(
  n: number,
  title: string,
  organization: string,
  region: string,
  category: string,
  hoursAgo: number,
  status = 'published',
) {
  return {
    id: `art_${String(n).padStart(12, '0')}`,
    title,
    organization,
    category,
    region,
    relevance: status === 'published' ? 0.9 : 0.52,
    status,
    published_at: null,
    collected_at: new Date(OVERVIEW_NOW.getTime() - hoursAgo * HOUR).toISOString(),
  };
}

/** The mock's 7 newest articles, newest first, the fourth held for review. */
export const LATEST_ARTICLES = [
  latest(
    1,
    'Shepherd’s 5K returns to Lakefront Promenade this May',
    'Shepherd’s 5K',
    'mississauga',
    'races-challenges',
    2,
  ),
  latest(
    2,
    'Grace Barbell Club opens second location in Leslieville',
    'Grace Barbell Club',
    'toronto',
    'gyms-studios',
    3,
  ),
  latest(
    3,
    'Men’s Iron Sharpens Iron bootcamp launches at Crossroads Church',
    'Crossroads Church Fitness Ministry',
    'vaughan',
    'ministry-programs',
    5,
  ),
  latest(
    4,
    'Toronto youth pastors challenge congregations to 100 km in March',
    'Ontario Christian Coaches Network',
    'toronto',
    'races-challenges',
    6,
    'needs_review',
  ),
  latest(
    5,
    'Temple Fit Markham adds prenatal strength classes',
    'Temple Fit Markham',
    'toronto',
    'gyms-studios',
    8,
  ),
  latest(
    6,
    'Faithful Fuel publishes Lenten meal-prep guide for athletes',
    'Faithful Fuel Nutrition',
    'mississauga',
    'nutrition',
    11,
  ),
  latest(
    7,
    'Torch Athletics hosts free sports camp for Brampton newcomers',
    'Torch Athletics Brampton',
    'toronto',
    'events',
    14,
  ),
];

/** A page of the newest articles with the whole total. */
export function latestPage(items: readonly unknown[] = LATEST_ARTICLES, total = 18432) {
  return { items, total, page: 1, page_size: 7 };
}

/** An open issue as `GET /admin/v1/issues` serves it. */
export function openIssue(fields: Record<string, unknown> = {}) {
  return {
    id: 'iss_000000000001',
    run_id: 'run_2418',
    severity: 'error',
    kind: 'source_not_found',
    subject_type: 'source',
    subject_id: 'src_000000000001',
    subject_name: 'Redeemed Run Club Toronto',
    detail: 'Nothing was found at https://redeemedrun.ca/feed.',
    consecutive_runs: 1,
    since: '2026-11-28T11:01:00Z',
    ...fields,
  };
}

/** The mock's 3 open issues, most severe first as served. */
export const OPEN_ISSUES = [
  openIssue(),
  openIssue({
    id: 'iss_000000000002',
    kind: 'source_timeout',
    subject_id: 'src_000000000002',
    subject_name: 'Covenant Cycling Collective',
    detail: 'The source did not answer within 30s on 3 attempts.',
    consecutive_runs: 4,
    since: '2026-11-23T17:02:00Z',
  }),
  openIssue({
    id: 'iss_000000000003',
    severity: 'warning',
    kind: 'items_held',
    subject_id: 'src_000000000003',
    subject_name: 'Google News · “Toronto church fitness”',
    detail: '6 items held for review: their relevance score is between 0.45 and 0.60.',
    since: '2026-11-28T11:03:00Z',
  }),
];

/** A page of open issues with how many are open in all. */
export function issuePage(items: readonly unknown[] = OPEN_ISSUES, total = items.length) {
  return { items, total, page_size: 5 };
}
