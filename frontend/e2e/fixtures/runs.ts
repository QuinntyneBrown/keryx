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
  const row = RUN_HISTORY.find((run) => run.id === id) ?? RUN_HISTORY[0];
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
