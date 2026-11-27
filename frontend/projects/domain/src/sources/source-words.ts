import type { SourceHealth, SourceKind } from '@kx/api';

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

/**
 * The health cell's words (L2-062). The failure reason's words and the run count arrive with the
 * runs that record them (journey 17); until then a failing source reads "Failing".
 */
export function sourceHealthLabel(health: SourceHealth, held: number): SourceHealthLabel {
  switch (health) {
    case 'failing':
      return { tone: 'danger', prefix: null, text: 'Failing' };
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

const NUMBER = new Intl.NumberFormat('en-CA');

/** The Sources header line: "64 sources across the GTA · 2 failing · 1 need a look". */
export function sourcesSummary({ all, failing, needsLook }: SourceCounts): string {
  if (all === 0) return 'No sources yet.';
  const parts = [`${NUMBER.format(all)} ${all === 1 ? 'source' : 'sources'} across the GTA`];
  if (failing > 0) parts.push(`${NUMBER.format(failing)} failing`);
  if (needsLook > 0) parts.push(`${NUMBER.format(needsLook)} need a look`);
  return parts.join(' · ');
}
