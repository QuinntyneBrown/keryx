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

/** How far a run in progress has got, for its stages' lines; null once it has finished. */
export interface RunProgress {
  checked: number;
  total: number;
  /** When the run the API still serves started, as "6:00 AM"; null when it serves none. */
  serving: string | null;
}

/**
 * How long the running stage has run: the run's time so far less what the stages before it
 * took, never negative.
 */
export function runningStageMs(
  stages: readonly Pick<StageView, 'status' | 'duration_ms'>[],
  elapsedMs: number,
): number {
  const before = stages
    .filter((stage) => stage.status !== 'running' && stage.status !== 'not_run')
    .reduce((sum, stage) => sum + stage.duration_ms, 0);
  return Math.max(0, elapsedMs - before);
}

/**
 * A stage's time: its duration, the running stage's time so far, or "—" for a stage that did
 * not run.
 */
export function stageTime(result: StageView, runningMs = 0): string {
  if (result.status === 'not_run') return '—';
  return formatStageDuration(result.status === 'running' ? runningMs : result.duration_ms);
}

/**
 * What a stage's line says (L2-057 AC2, AC3): its summary when it finished; the generic reason
 * the run recorded when it failed; "Skipped" when a failure stopped it, publish adding that the
 * API still serves the previous run. While the run is in progress (L2-058 AC1) a running fetch
 * tells the sources checked so far, and the stages still to come are "Waiting", publish naming
 * the run the API serves meanwhile.
 */
export function stageDetail(result: StageView, progress: RunProgress | null = null): string {
  switch (result.status) {
    case 'failed':
      return result.reason ?? 'This stage failed.';
    case 'not_run':
      if (progress) {
        return result.stage === 'publish' && progress.serving
          ? `Waiting — API is serving the ${progress.serving} run`
          : 'Waiting';
      }
      return result.stage === 'publish' ? 'Skipped — API still serving previous run' : 'Skipped';
    case 'running':
      if (progress && result.stage === 'fetch') {
        return `${progress.checked} of ${progress.total} sources checked`;
      }
      return stageSummary(result.stage, result.counts);
    default:
      return stageSummary(result.stage, result.counts);
  }
}
