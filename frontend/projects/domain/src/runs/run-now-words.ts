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
