import {
  formatClockTime,
  formatCountdown,
  formatDuration,
  type RunOutcome,
} from '../overview/run-words';

const OUTCOMES: Record<RunOutcome, string> = {
  succeeded: 'Succeeded',
  completed_with_issues: 'Completed with issues',
  failed: 'Failed',
  running: 'Running',
};

/** A run's outcome as the word beside its icon (L2-056 AC1), so colour is never the only sign. */
export function runOutcomeLabel(outcome: RunOutcome): string {
  return OUTCOMES[outcome];
}

/** How long a run has been going: never negative, even when this clock lags the server's. */
export function runElapsedMs(startedAt: string, now: Date): number {
  return Math.max(0, now.getTime() - Date.parse(startedAt));
}

/** The parts of a run its line in the history is made of. */
export interface RunLineView {
  outcome: RunOutcome;
  trigger: string;
  added: number;
  duration_ms: number | null;
  started_at: string;
  sources_checked: number;
  sources_total: number;
}

/**
 * A run's line in the history (L2-056 AC1): "47 new · 4m 12s", "· manual" when staff started it,
 * and "Failed · 0m 48s" for a run that failed, which stored nothing. A run in progress tells how
 * far it has got, "38 of 64 sources checked · 1m 32s so far" (L2-058 AC1).
 */
export function runLineMeta(run: RunLineView, now: Date): string {
  const manual = run.trigger === 'manual' ? ' · manual' : '';
  if (run.outcome === 'running') {
    const so = formatDuration(runElapsedMs(run.started_at, now));
    return `${run.sources_checked} of ${run.sources_total} sources checked · ${so} so far${manual}`;
  }
  const took = formatDuration(run.duration_ms ?? 0);
  const what = run.outcome === 'failed' ? 'Failed' : `${run.added} new`;
  return `${what} · ${took}${manual}`;
}

/**
 * How a run ran, under its heading: "Schedule · took 4m 12s", or "Manual · running for 1m 32s"
 * while it is in progress.
 */
export function runHowLine(
  run: Pick<RunLineView, 'trigger' | 'outcome' | 'duration_ms' | 'started_at'>,
  now: Date,
): string {
  const how = run.trigger === 'manual' ? 'Manual' : 'Schedule';
  return run.outcome === 'running'
    ? `${how} · running for ${formatDuration(runElapsedMs(run.started_at, now))}`
    : `${how} · took ${formatDuration(run.duration_ms ?? 0)}`;
}

/** The flag of a run whose sources failed, "2 failed"; none when every source answered. */
export function runFailedFlag(sourcesFailed: number): string | null {
  return sourcesFailed > 0 ? `${sourcesFailed} failed` : null;
}

/**
 * An issue's streak (L2-057 AC4): "Failing 4 runs in a row" for an error seen in more than one
 * run in a row; none for a first occurrence, or for a warning, which is not a failure.
 */
export function issueStreakLabel(
  severity: 'error' | 'warning',
  consecutiveRuns: number,
): string | null {
  return severity === 'error' && consecutiveRuns > 1
    ? `Failing ${consecutiveRuns} runs in a row`
    : null;
}

/** The schedule as Runs' header says it. */
export interface ScheduleView {
  next_run_at: string;
  interval_hours: number;
  time_zone: string;
}

/**
 * The schedule line (L2-056 AC1): "Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)", with
 * "every hour" for a one-hour interval.
 */
export function scheduleLine(schedule: ScheduleView, now: Date): string {
  const every =
    schedule.interval_hours === 1 ? 'every hour' : `every ${schedule.interval_hours} hours`;
  const at = formatClockTime(schedule.next_run_at, schedule.time_zone);
  return `Scheduled ${every} · next at ${at} (${formatCountdown(now, schedule.next_run_at)})`;
}
