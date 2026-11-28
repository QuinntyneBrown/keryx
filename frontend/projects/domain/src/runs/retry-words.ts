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

/** A source that failed, by its organization's name and its issue's kind. */
export interface FailedSource {
  name: string;
  kind: string;
}

/** A run's failed sources: its `error` issues about a source that names one. */
export function failedSources(
  issues: readonly {
    severity: string;
    kind: string;
    subject_type: string;
    subject_name: string | null;
  }[],
): FailedSource[] {
  return issues
    .filter((i) => i.severity === 'error' && i.subject_type === 'source' && i.subject_name)
    .map((i) => ({ name: i.subject_name as string, kind: i.kind }));
}

/** What a failed source did, as said once the retry has ended: [first time, still]. */
function failure(kind: string): [string, string] {
  switch (kind) {
    case 'source_not_found':
      return ['returned 404', 'still returns 404'];
    case 'source_timeout':
      return ['timed out', 'still times out'];
    case 'source_blocked':
      return ['was blocked', 'is still blocked'];
    default:
      return ['answered with nothing Keryx can read', 'still answers with nothing Keryx can read'];
  }
}

/** "A returned 404 and B timed out", "A …, B … and 1 more failed". */
function failures(sources: readonly FailedSource[], still: boolean): string {
  const said = sources.slice(0, 2).map((s) => `${s.name} ${failure(s.kind)[still ? 1 : 0]}`);
  const more = sources.length - said.length;
  return more > 0 ? `${said.join(', ')} and ${more} more failed` : said.join(' and ');
}

/**
 * How a retry ended (L2-059 AC2, AC3): which of the failed sources responded and which still
 * fail, with "Edit source" or "Edit sources" for those that do.
 */
export function summarizeRetry(
  before: readonly FailedSource[],
  after: readonly FailedSource[],
): {
  tone: 'success' | 'warning' | 'danger';
  title: string;
  text: string;
  action: 'Edit source' | 'Edit sources' | null;
} {
  const failing = new Set(after.map((s) => s.name));
  const recovered = before.filter((s) => !failing.has(s.name)).map((s) => s.name);
  const total = before.length;
  const action = after.length === 1 ? 'Edit source' : 'Edit sources';
  if (after.length === 0) {
    return {
      tone: 'success',
      title: total === 1 ? 'The source recovered' : `All ${total} sources recovered`,
      text: `${namedFew(recovered, recovered.length)} responded.`,
      action: null,
    };
  }
  if (recovered.length === 0) {
    return {
      tone: 'danger',
      title:
        total === 1
          ? 'The source failed again'
          : total === 2
            ? 'Both sources failed again'
            : `All ${total} sources failed again`,
      text: `${failures(after, false)}.`,
      action,
    };
  }
  return {
    tone: 'warning',
    title: `${recovered.length} of ${total} sources recovered`,
    text: `${namedFew(recovered, recovered.length)} responded. ${failures(after, true)}.`,
    action,
  };
}
