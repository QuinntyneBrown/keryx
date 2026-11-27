import { dayAndTime } from '../articles/article-words';
import type { RunOutcome } from './run-words';

/** The parts of a run its bar is drawn from. */
export interface RunPoint {
  id: string;
  started_at: string;
  outcome: RunOutcome;
  added: number;
}

/** One bar of the run chart (docs/design-system/components/run-chart.html). */
export interface RunBar {
  id: string;
  status: 'success' | 'warning' | 'failed' | 'running';
  /** Its height, 0 to 100, against the run that added the most. A failed bar is a stub. */
  value: number;
  /** What a screen reader says: "Today 6:00 AM: 47 new", "…: failed" or "…: running". */
  label: string;
  /** Its tooltip: "Today 6:00 AM · 47 new". */
  title: string;
}

const STATUS: Record<RunOutcome, RunBar['status']> = {
  succeeded: 'success',
  completed_with_issues: 'warning',
  failed: 'failed',
  running: 'running',
};

/**
 * The run chart's bars from runs served newest first (L2-044): oldest first, each as tall as its
 * new articles against the most any of them added, and labelled by its time and count, or by
 * "failed" or "running", so the outcome is never carried by colour alone.
 */
export function runBars(runs: readonly RunPoint[], now: Date, timeZone: string): RunBar[] {
  const most = Math.max(...runs.map((run) => run.added));
  return [...runs].reverse().map((run) => {
    const { day, time } = dayAndTime(run.started_at, now, timeZone);
    const when = `${day} ${time}`;
    const what =
      run.outcome === 'failed' || run.outcome === 'running' ? run.outcome : `${run.added} new`;
    return {
      id: run.id,
      status: STATUS[run.outcome],
      value: most === 0 ? 0 : Math.round((run.added / most) * 100),
      label: `${when}: ${what}`,
      title: `${when} · ${what}`,
    };
  });
}
