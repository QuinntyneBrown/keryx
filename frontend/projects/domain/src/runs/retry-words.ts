/**
 * Whether a run offers "Retry failed sources": one that finished with failed sources, or one
 * that failed, whose retry starts a new run (L2-059 AC4, L2-008 AC5).
 */
export function canRetryFailedSources(run: { outcome: string; sources_failed: number }): boolean {
  return run.outcome === 'failed' || (run.outcome !== 'running' && run.sources_failed > 0);
}

/** Up to two names, then how many more: "A and B", "A, B and 2 more". */
export function namedFew(names: readonly string[], total: number): string {
  const named = names.slice(0, 2);
  const more = total - named.length;
  return more > 0 ? `${named.join(', ')} and ${more} more` : named.join(' and ');
}

/** "Retrying 2 failed sources" and which they are (L2-059 AC1). */
export function retryStartedNotice(
  failed: number,
  names: readonly string[],
): { title: string; text: string } {
  const results = 'Results are added to this run.';
  return {
    title: `Retrying ${failed} failed ${failed === 1 ? 'source' : 'sources'}`,
    text: names.length ? `${namedFew(names, failed)}. ${results}` : results,
  };
}
