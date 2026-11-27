/** How a run ended, or `running`, as the admin API serves it. */
export type RunOutcome = 'running' | 'succeeded' | 'completed_with_issues' | 'failed';

/** The parts of a run its headline is worked out from. */
export interface RunSummaryView {
  outcome: RunOutcome;
  failure_code: string | null;
  sources_ok: number;
  sources_total: number;
}

/** What a failed run's headline says for each failure code; any other reads "stopped unexpectedly". */
const FAILED: Record<string, string> = {
  store_failed: 'database unreachable',
  interrupted: 'interrupted by a restart',
};

/**
 * A run's outcome in words (L2-043 AC1–AC4): the status card's headline, which carries the
 * outcome as text so colour is never the only sign of it (AC5).
 */
export function runHeadline(run: RunSummaryView): string {
  switch (run.outcome) {
    case 'succeeded':
      return 'Completed — all sources collected';
    case 'completed_with_issues': {
      const failing = run.sources_total - run.sources_ok;
      if (failing <= 0) return 'Completed with issues';
      return `Completed with ${failing} failing ${failing === 1 ? 'source' : 'sources'}`;
    }
    case 'failed':
      return `Run failed — ${FAILED[run.failure_code ?? ''] ?? 'stopped unexpectedly'}`;
    default:
      return 'Collecting news now…';
  }
}

/** The tone of a run's icon and bar. */
export function runTone(outcome: RunOutcome): 'success' | 'warning' | 'danger' | 'info' {
  switch (outcome) {
    case 'succeeded':
      return 'success';
    case 'completed_with_issues':
      return 'warning';
    case 'failed':
      return 'danger';
    default:
      return 'info';
  }
}

/** A run's length: "4m 12s" and "4m 03s", or "1h 2m" from an hour. */
export function formatDuration(ms: number): string {
  const seconds = Math.floor(ms / 1000);
  const minutes = Math.floor(seconds / 60);
  if (minutes >= 60) return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
  return `${minutes}m ${String(seconds % 60).padStart(2, '0')}s`;
}

/** A time of day in a zone: "12:00 PM". */
export function formatClockTime(at: string | Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-US', { timeZone, hour: 'numeric', minute: '2-digit' }).format(
    new Date(at),
  );
}

/**
 * How long until a time, in whole minutes rounded up as a countdown reads: "in 2h 14m", "in 14m",
 * "in under a minute", or "due now" once it has passed.
 */
export function formatCountdown(now: Date, at: string | Date): string {
  const ms = new Date(at).getTime() - now.getTime();
  if (ms <= 0) return 'due now';
  if (ms < 60_000) return 'in under a minute';
  const minutes = Math.ceil(ms / 60_000);
  const hours = Math.floor(minutes / 60);
  return hours > 0 ? `in ${hours}h ${minutes % 60}m` : `in ${minutes}m`;
}
