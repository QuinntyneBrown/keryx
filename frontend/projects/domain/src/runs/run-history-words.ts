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

/** The parts of a finished run its line in the history is made of. */
export interface RunLineView {
  outcome: RunOutcome;
  trigger: string;
  added: number;
  duration_ms: number | null;
}

/**
 * A finished run's line in the history (L2-056 AC1): "47 new · 4m 12s", "· manual" when staff
 * started it, and "Failed · 0m 48s" for a run that failed, which stored nothing.
 */
export function runLineMeta(run: RunLineView): string {
  const took = formatDuration(run.duration_ms ?? 0);
  const what = run.outcome === 'failed' ? 'Failed' : `${run.added} new`;
  return `${what} · ${took}${run.trigger === 'manual' ? ' · manual' : ''}`;
}

/** The flag of a run whose sources failed, "2 failed"; none when every source answered. */
export function runFailedFlag(sourcesFailed: number): string | null {
  return sourcesFailed > 0 ? `${sourcesFailed} failed` : null;
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
