import type { SourceHealth, SourceKind } from '@kx/api';
import { formatCollectedAt, pageRange } from '../articles/article-words';

const NUMBER = new Intl.NumberFormat('en-CA');

const KIND_LABELS: Record<SourceKind, string> = {
  rss: 'RSS',
  website: 'Website',
  instagram: 'Instagram',
  eventbrite: 'Eventbrite',
  news_query: 'News query',
};

/** The name of a kind as staff read it: "RSS", "News query". */
export function sourceKindLabel(kind: SourceKind): string {
  return KIND_LABELS[kind];
}

/**
 * Where Keryx reads a source, as the Sources table shows it: a URL without its scheme and
 * "www.", a news query's text as given. Shown as text, never HTML.
 */
export function formatSourceAddress(kind: SourceKind, address: string): string {
  if (kind === 'news_query') return address;
  const match = /^https?:\/\/(?:www\.)?(.*)$/i.exec(address);
  if (!match) return address;
  const rest = match[1].replace(/\/$/, '');
  const slash = rest.search(/[/?#]/);
  const host = slash < 0 ? rest : rest.slice(0, slash);
  return host.toLowerCase() + (slash < 0 ? '' : rest.slice(slash));
}

/** How a health reads: its tone, a hidden category prefix when the text does not say it, and the text. */
export interface SourceHealthLabel {
  tone: 'healthy' | 'warning' | 'danger';
  prefix: string | null;
  text: string;
}

/** A failed fetch's reason, as the health cell names it (L2-002's fixed list). */
const FAILURE_WORDS: Record<string, string> = {
  not_found: '404 Not Found',
  timeout: 'Timeout',
  blocked_by_robots: 'Blocked by robots.txt',
  refused: 'Refused',
  invalid_content: 'Unreadable content',
  unsafe_address: 'Not a public address',
};

/**
 * The health cell's words (L2-062 AC3): "Failing: 404 Not Found", and after more than one failed
 * run in a row " · 4 runs", as Runs counts a streak. A reason Keryx does not know reads "Failing".
 */
export function sourceHealthLabel(
  health: SourceHealth,
  held: number,
  reason: string | null = null,
  failures = 0,
): SourceHealthLabel {
  switch (health) {
    case 'failing': {
      const words = reason ? FAILURE_WORDS[reason] : undefined;
      if (!words) return { tone: 'danger', prefix: null, text: 'Failing' };
      const streak = failures > 1 ? ` · ${NUMBER.format(failures)} runs` : '';
      return { tone: 'danger', prefix: 'Failing: ', text: `${words}${streak}` };
    }
    case 'needs_look':
      return held > 0
        ? { tone: 'warning', prefix: 'Needs a look: ', text: `${held} held for review` }
        : { tone: 'warning', prefix: null, text: 'Needs a look' };
    default:
      return { tone: 'healthy', prefix: null, text: 'Healthy' };
  }
}

export interface SourceCounts {
  all: number;
  failing: number;
  needsLook: number;
}

/** "Showing 1–12 of 64 sources, failing first." under the Sources table (L2-062 AC1). */
export function sourcesRange(page: number, pageSize: number, total: number): string {
  return `${pageRange(page, pageSize, total, ['source', 'sources'])}, failing first.`;
}

/** When a source was last fetched, "Today, 6:01 AM", or "Not yet" before its first run. */
export function sourceLastFetched(at: string | null, now: Date, timeZone: string): string {
  return at ? formatCollectedAt(at, now, timeZone) : 'Not yet';
}

/** The Sources header line: "64 sources across the GTA · 2 failing · 1 need a look". */
export function sourcesSummary({ all, failing, needsLook }: SourceCounts): string {
  if (all === 0) return 'No sources yet.';
  const parts = [`${NUMBER.format(all)} ${all === 1 ? 'source' : 'sources'} across the GTA`];
  if (failing > 0) parts.push(`${NUMBER.format(failing)} failing`);
  if (needsLook > 0) parts.push(`${NUMBER.format(needsLook)} need a look`);
  return parts.join(' · ');
}
