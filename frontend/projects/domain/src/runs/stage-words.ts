/** The six pipeline stages, in the order they run (L2-007). */
export const PIPELINE_STAGES = [
  'fetch',
  'parse',
  'classify',
  'deduplicate',
  'store',
  'publish',
] as const;

const NAMES: Record<string, string> = {
  fetch: 'Fetch sources',
  parse: 'Parse & extract',
  classify: 'Classify relevance',
  deduplicate: 'Deduplicate',
  store: 'Store',
  publish: 'Publish to API',
};

/** A stage's name as staff read it; a stage Keryx does not know shows as itself. */
export function stageName(stage: string): string {
  return Object.hasOwn(NAMES, stage) ? NAMES[stage] : stage;
}

const WORDS: Record<string, string> = {
  done: 'Done',
  done_with_warnings: 'Done with warnings',
  failed: 'Failed',
  running: 'Running',
  not_run: 'Not run',
};

/** A stage's status in words (L2-057 AC2), so its icon's colour is never the only sign. */
export function stageStatusLabel(status: string): string {
  return Object.hasOwn(WORDS, status) ? WORDS[status] : 'Not run';
}

/** The timeline step's state for a stage's status. */
export function stageState(status: string): 'ok' | 'warn' | 'failed' | 'running' | 'skipped' {
  switch (status) {
    case 'done':
      return 'ok';
    case 'done_with_warnings':
      return 'warn';
    case 'failed':
      return 'failed';
    case 'running':
      return 'running';
    default:
      return 'skipped';
  }
}

/** A stage's length as minutes and seconds: "2:48", "0:06", "10:15". */
export function formatStageDuration(ms: number): string {
  const seconds = Math.floor(ms / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

/** One noun, singular for one: "1 duplicate", "18 duplicates". */
function count(n: number, one: string, many = `${one}s`): string {
  return `${n} ${n === 1 ? one : many}`;
}

/**
 * What a finished stage did, from its counts (L2-057 AC2): "62 of 64 sources fetched · 2 failed",
 * "189 candidate items from feeds, pages and posts", "53 relevant · 6 held for review ·
 * 130 off-topic", "18 duplicates merged with existing articles", "Saved to articles table" and
 * "47 new articles available at /v1/articles".
 */
export function stageSummary(stage: string, counts: Record<string, number>): string {
  const n = (key: string) => counts[key] ?? 0;
  switch (stage) {
    case 'fetch': {
      const all = n('sources_ok') + n('sources_failed');
      const failed = n('sources_failed') > 0 ? ` · ${n('sources_failed')} failed` : '';
      return `${n('sources_ok')} of ${all} sources fetched${failed}`;
    }
    case 'parse':
      return `${count(n('items'), 'candidate item')} from feeds, pages and posts`;
    case 'classify':
      return `${n('relevant')} relevant · ${n('held')} held for review · ${n('off_topic')} off-topic`;
    case 'deduplicate': {
      const rejected = n('rejected') > 0 ? ` · ${n('rejected')} rejected` : '';
      return `${count(n('duplicates'), 'duplicate')} merged with existing articles${rejected}`;
    }
    case 'store':
      return 'Saved to articles table';
    case 'publish':
      return `${count(n('published'), 'new article')} available at /v1/articles`;
    default:
      return '';
  }
}

/** The parts of a stage result its line in the pipeline is made of. */
export interface StageView {
  stage: string;
  status: string;
  duration_ms: number;
  counts: Record<string, number>;
  reason: string | null;
}

/** A stage's time: its duration, or "—" for a stage that did not run. */
export function stageTime(result: StageView): string {
  return result.status === 'not_run' ? '—' : formatStageDuration(result.duration_ms);
}

/**
 * What a stage's line says (L2-057 AC2, AC3): its summary when it finished; the generic reason
 * the run recorded when it failed; "Skipped" when a failure stopped it, publish adding that the
 * API still serves the previous run.
 */
export function stageDetail(result: StageView): string {
  switch (result.status) {
    case 'failed':
      return result.reason ?? 'This stage failed.';
    case 'not_run':
      return result.stage === 'publish' ? 'Skipped — API still serving previous run' : 'Skipped';
    default:
      return stageSummary(result.stage, result.counts);
  }
}
