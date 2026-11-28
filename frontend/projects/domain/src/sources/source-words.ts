import type { SourceHealth, SourceKind } from '@kx/api';
import { dayAndTime, formatCollectedAt, pageRange } from '../articles/article-words';

const NUMBER = new Intl.NumberFormat('en-CA');

const KIND_LABELS: Record<SourceKind, string> = {
  rss: 'RSS',
  website: 'Website',
  instagram: 'Instagram',
  eventbrite: 'Eventbrite',
  news_query: 'News query',
};

/** Every kind, in the order the kind chips show them (L2-062 AC4). */
export const SOURCE_KINDS: readonly SourceKind[] = [
  'rss',
  'website',
  'instagram',
  'eventbrite',
  'news_query',
];

/** A kind inside a sentence, "23 RSS sources": the label, lower-cased unless a name. */
const KIND_NOUNS: Record<SourceKind, string> = {
  rss: 'RSS',
  website: 'website',
  instagram: 'Instagram',
  eventbrite: 'Eventbrite',
  news_query: 'news query',
};

/** What one source of a kind is: "an Eventbrite organiser". */
const KIND_ONE: Record<SourceKind, string> = {
  rss: 'an RSS feed',
  website: 'a website',
  instagram: 'an Instagram account',
  eventbrite: 'an Eventbrite organiser',
  news_query: 'a news query',
};

/** The kind a URL names, or null for every kind and for a word Keryx does not know. */
export function sourceKindOf(value: string | null): SourceKind | null {
  return SOURCE_KINDS.find((kind) => kind === value) ?? null;
}

/**
 * The no-results state for a kind no source is (L2-062 AC5): "No Eventbrite sources" and "None
 * of the 64 sources is an Eventbrite organiser. Show every type, or add one."
 */
export function sourcesNoResults(kind: SourceKind, all: number): { title: string; text: string } {
  const none =
    all === 1
      ? `The one source isn’t ${KIND_ONE[kind]}.`
      : `None of the ${NUMBER.format(all)} sources is ${KIND_ONE[kind]}.`;
  return { title: `No ${KIND_NOUNS[kind]} sources`, text: `${none} Show every type, or add one.` };
}

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
export function sourcesRange(
  page: number,
  pageSize: number,
  total: number,
  kind: SourceKind | null = null,
): string {
  const noun = kind ? `${KIND_NOUNS[kind]} ` : '';
  const range = pageRange(page, pageSize, total, [`${noun}source`, `${noun}sources`]);
  return `${range}, failing first.`;
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

/**
 * The site a source's "Open <host>" item opens (L2-063 AC4): its host and its address, only for an
 * `http` or `https` URL. A news query, or any other address, has none.
 */
export function sourceSiteLink(address: string): { host: string; url: string } | null {
  let url: URL;
  try {
    url = new URL(address);
  } catch {
    return null;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return null;
  return { host: url.hostname.replace(/^www\./, ''), url: url.href };
}

/**
 * The alert above "Edit source" for a failing source (L2-065 AC1): "Last fetch failed today at
 * 6:01 AM" and "GET https://redeemedrun.ca/feed → 404 Not Found. Check the address below.";
 * null for a source that is not failing or was never fetched.
 */
export function lastFetchFailure(
  source: {
    kind: SourceKind;
    address: string;
    health: SourceHealth;
    failure_reason: string | null;
    last_fetched_at: string | null;
  },
  now: Date,
  timeZone: string,
): { title: string; text: string } | null {
  if (source.health !== 'failing' || !source.last_fetched_at) return null;
  const { day, time } = dayAndTime(source.last_fetched_at, now, timeZone);
  const when = day === 'Today' || day === 'Yesterday' ? day.toLowerCase() : `on ${day}`;
  const words = source.failure_reason ? FAILURE_WORDS[source.failure_reason] : undefined;
  const query = source.kind === 'news_query';
  const what = query ? 'The news search' : `GET ${source.address}`;
  const failed = words ? `${what} → ${words}.` : `${what} failed.`;
  return {
    title: `Last fetch failed ${when} at ${time}`,
    text: `${failed} Check the ${query ? 'query' : 'address'} below.`,
  };
}

/**
 * What "Remove source" says stays (L2-065 AC3): "The 31 articles already collected from this
 * source stay in the database and in the API, …". Without a count, as when it could not be read,
 * it says the same without the number.
 */
export function removeSourceText(articles: number | null): string {
  const again = 'You can add the source again later.';
  if (articles === 0) return `No articles have been collected from this source. ${again}`;
  if (articles === 1) {
    return `The 1 article already collected from this source stays in the database and in the API, with its link back to the source. ${again}`;
  }
  const count = articles === null ? 'The articles' : `The ${NUMBER.format(articles)} articles`;
  return `${count} already collected from this source stay in the database and in the API, each with its link back to the source. ${again}`;
}
