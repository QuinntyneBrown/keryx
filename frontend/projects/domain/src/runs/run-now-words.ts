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
