import { formatClockTime } from '../overview/run-words';

/**
 * The sentence under "Collection started" (L2-047 AC1): the sources the run will check and, when
 * a run has finished before, about how long that took, in whole minutes and at least one.
 */
export function collectionStartedText(sourcesTotal: number, estimatedMs: number | null): string {
  const checking = `Checking ${sourcesTotal} ${sourcesTotal === 1 ? 'source' : 'sources'}.`;
  if (estimatedMs === null) return checking;
  const minutes = Math.max(1, Math.round(estimatedMs / 60_000));
  return `${checking} This usually takes about ${minutes} ${minutes === 1 ? 'minute' : 'minutes'}.`;
}

/**
 * Runs' header while a collection is in progress (L2-058 AC4): "A collection is running now ·
 * started 9:46 AM". The mock's "by Daniel Adeyemi" is left out: a run records no starter.
 */
export function collectionRunningLine(startedAt: string, timeZone: string): string {
  return `A collection is running now · started ${formatClockTime(startedAt, timeZone)}`;
}

/** What the outcome notification reads of a run that has ended. */
export interface EndedRun {
  outcome: string;
  added: number;
  sources_failed: number;
  sources_total: number;
  stages: readonly { stage: string; status: string }[];
  issues: readonly { severity: string; subject_type: string; subject_name: string | null }[];
}

/** The outcome notification's words and which action it offers (L2-076 AC2–AC4). */
export interface EndedNotice {
  tone: 'success' | 'warning' | 'danger';
  title: string;
  text: string;
  action: 'view' | 'retry';
}

const SERVING_PREVIOUS = 'The API is still serving the previous run.';

function articles(count: number): string {
  return `${count} new ${count === 1 ? 'article' : 'articles'}`;
}

/**
 * How a run that has ended is announced: finished, finished with failing sources (naming up to
 * two, as their sources published them), or failed, with the database named when the store stage
 * failed.
 */
export function collectionEndedNotice(run: EndedRun): EndedNotice {
  if (run.outcome === 'failed') {
    const storeFailed = run.stages.some((s) => s.stage === 'store' && s.status === 'failed');
    return {
      tone: 'danger',
      title: 'Collection failed',
      text: storeFailed
        ? `Keryx couldn’t reach its database, so nothing was stored. ${SERVING_PREVIOUS}`
        : `The run stopped before anything was stored. ${SERVING_PREVIOUS}`,
      action: 'retry',
    };
  }
  const failing = run.sources_failed;
  if (failing === 0) {
    return {
      tone: 'success',
      title: 'Collection finished',
      text: `${articles(run.added)} from ${run.sources_total} ${run.sources_total === 1 ? 'source' : 'sources'}.`,
      action: 'view',
    };
  }
  const names = run.issues
    .filter((i) => i.severity === 'error' && i.subject_type === 'source' && i.subject_name)
    .map((i) => i.subject_name as string);
  const named = names.slice(0, 2);
  const more = failing - named.length;
  const who =
    named.length === 0
      ? ''
      : more > 0
        ? ` ${named.join(', ')} and ${more} more didn’t respond.`
        : ` ${named.join(' and ')} didn’t respond.`;
  return {
    tone: 'warning',
    title: `Collection finished with ${failing} failing ${failing === 1 ? 'source' : 'sources'}`,
    text: `${articles(run.added)}.${who}`,
    action: 'view',
  };
}
