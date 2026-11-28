// What the admin API answers for Runs (docs/mocks/pages/runs/), at Overview's now: 28 November
// 2026, 9:46 AM in Toronto.

/** One run of the list, with the mock's numbers. */
export function historyRun(
  id: string,
  started_at: string,
  outcome: string,
  added: number,
  duration_s: number,
  sources_failed = 0,
  trigger = 'schedule',
): Record<string, unknown> {
  return {
    id,
    trigger,
    started_at,
    finished_at: outcome === 'running' ? null : started_at,
    duration_ms: outcome === 'running' ? null : duration_s * 1000,
    outcome,
    added,
    sources_checked: 64,
    sources_total: 64,
    sources_failed,
  };
}

/** The mock's 14 runs, newest first: a failed run yesterday at noon, a manual one at 2:37 PM. */
export const RUN_HISTORY = [
  historyRun('run_2418', '2026-11-28T11:00:00Z', 'completed_with_issues', 47, 252, 2),
  historyRun('run_2417', '2026-11-28T05:00:00Z', 'succeeded', 12, 231),
  historyRun('run_2416', '2026-11-27T23:00:00Z', 'succeeded', 31, 243),
  historyRun('run_2415', '2026-11-27T19:37:00Z', 'succeeded', 8, 220, 0, 'manual'),
  historyRun('run_2414', '2026-11-27T17:00:00Z', 'failed', 0, 48),
  historyRun('run_2413', '2026-11-27T11:00:00Z', 'succeeded', 39, 262, 1),
  historyRun('run_2412', '2026-11-27T05:00:00Z', 'succeeded', 6, 238, 1),
  historyRun('run_2411', '2026-11-26T23:00:00Z', 'succeeded', 28, 250, 1),
  historyRun('run_2410', '2026-11-26T17:00:00Z', 'completed_with_issues', 22, 271, 4),
  historyRun('run_2409', '2026-11-26T11:00:00Z', 'succeeded', 41, 245),
  historyRun('run_2408', '2026-11-26T05:00:00Z', 'succeeded', 9, 229),
  historyRun('run_2407', '2026-11-25T23:00:00Z', 'succeeded', 35, 254),
  historyRun('run_2406', '2026-11-25T17:00:00Z', 'succeeded', 26, 259),
  historyRun('run_2405', '2026-11-25T11:00:00Z', 'succeeded', 19, 242),
];

/** 9:47:32 AM in Toronto: run_2419, started by hand at 9:46 AM, has run for 1m 32s. */
export const RUNNING_NOW = new Date('2026-11-28T14:47:32Z');

/** run_2419 in progress, as the running mock shows it: 38 of 64 sources checked. */
export const RUNNING_RUN = {
  ...historyRun('run_2419', '2026-11-28T14:46:00Z', 'running', 21, 0, 0, 'manual'),
  sources_checked: 38,
  sources_total: 64,
};

/** The shell's schedule: every 6 hours, next at 12:00 PM Toronto time, 2 sources failing. */
export function shellSchedule(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    next_run_at: '2026-11-28T17:00:00Z',
    interval_hours: 6,
    time_zone: 'America/Toronto',
    failing_sources: 2,
    ...overrides,
  };
}

/**
 * A run's detail as `GET /admin/v1/runs/{id}` serves it, from its row in the history: 64 sources,
 * those that did not fail OK, and run_2418's 18 duplicates. No stage and no issue unless given.
 */
export function runDetail(
  id: string,
  overrides: Record<string, unknown> = {},
): Record<string, unknown> {
  const row = [RUNNING_RUN, ...RUN_HISTORY].find((run) => run.id === id) ?? RUN_HISTORY[0];
  const failed = row['sources_failed'] as number;
  return {
    ...row,
    worker: 'collection',
    failure_reason: null,
    correlation_id: null,
    updated: 3,
    unchanged: 120,
    duplicates: id === 'run_2418' ? 18 : 5,
    held: 0,
    off_topic: 0,
    rejected: 0,
    sources_ok: 64 - failed,
    sources_checked: 64,
    sources_total: 64,
    stages: [],
    issues: [],
    ...overrides,
  };
}

/** One stage of a run's detail. */
export function stage(
  name: string,
  status: string,
  duration_s: number,
  counts: Record<string, number>,
  reason: string | null = null,
): Record<string, unknown> {
  return { stage: name, status, duration_ms: duration_s * 1000, counts, reason };
}

/** run_2418's six stages, as the mock shows them: two failed fetches and six items held. */
export const RUN_2418_STAGES = [
  stage('fetch', 'done_with_warnings', 168, { sources_ok: 62, sources_failed: 2 }),
  stage('parse', 'done', 21, { items: 189 }),
  stage('classify', 'done_with_warnings', 38, { relevant: 53, held: 6, off_topic: 130 }),
  stage('deduplicate', 'done', 6, { duplicates: 18, rejected: 0 }),
  stage('store', 'done', 3, { added: 47, updated: 3, unchanged: 120 }),
  stage('publish', 'done', 1, { published: 47 }),
];

/** A problem a run raised. */
export function runIssue(
  id: string,
  severity: string,
  kind: string,
  subject_name: string | null,
  detail: string,
  consecutive_runs = 1,
  subject_type = 'source',
): Record<string, unknown> {
  return {
    id,
    severity,
    kind,
    subject_type,
    subject_id: subject_type === 'source' ? `src_${id}` : null,
    subject_name,
    detail,
    consecutive_runs,
    since: '2026-11-28T11:01:00Z',
    muted: false,
  };
}

/** run_2418's three issues, as the mock shows them. */
export const RUN_2418_ISSUES = [
  runIssue(
    'iss_1',
    'error',
    'source_not_found',
    'Redeemed Run Club Toronto',
    'GET https://redeemedrun.ca/feed → 404 Not Found. The site may have moved its blog.',
  ),
  runIssue(
    'iss_2',
    'error',
    'source_timeout',
    'Covenant Cycling Collective',
    'Events page did not respond within 30s on 3 retries.',
    4,
  ),
  runIssue(
    'iss_3',
    'warning',
    'items_held',
    'Google News · “Toronto church fitness”',
    'Relevance score between 0.45 and 0.60 — may not be Christian fitness content.',
    // Held three runs in a row; a warning is not a failure, so it shows no streak.
    3,
  ),
];

/** run_2419's detail mid-run: fetch running with 21 new so far, the other stages waiting. */
export function runningDetail(): Record<string, unknown> {
  return runDetail('run_2419', {
    duplicates: 4,
    sources_ok: 38,
    sources_checked: 38,
    sources_total: 64,
    stages: [
      stage('fetch', 'running', 0, {}),
      ...['parse', 'classify', 'deduplicate', 'store', 'publish'].map((name) =>
        stage(name, 'not_run', 0, {}),
      ),
    ],
  });
}

/** `POST /admin/v1/runs` answering 202: run_2419 will check 64 sources; the last took 4m 12s. */
export const RUN_STARTED = { id: 'run_2419', sources_total: 64, estimated_duration_ms: 252_000 };
