import type { PublicationStatus } from './publication-status';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const counts = new Intl.NumberFormat('en-CA');

/**
 * How long ago something happened: minutes under an hour, hours under a day, otherwise days with
 * a thousands separator ("1,204d ago"). A time a little ahead of the browser's clock reads as now.
 */
export function formatRelativeAge(at: string | Date, now: Date): string {
  const elapsed = Math.max(0, now.getTime() - new Date(at).getTime());
  if (elapsed < HOUR) return `${Math.floor(elapsed / MINUTE)}m ago`;
  if (elapsed < DAY) return `${Math.floor(elapsed / HOUR)}h ago`;
  return `${counts.format(Math.floor(elapsed / DAY))}d ago`;
}

/** A relevance score to two decimals ("0.94"). */
export function formatRelevance(score: number): string {
  return score.toFixed(2);
}

const LABELS: Record<PublicationStatus, string> = {
  published: 'Published',
  needs_review: 'Needs review',
  hidden: 'Hidden',
};

/** A status as the word staff read; a badge shows the word, never colour alone. */
export function articleStatusLabel(status: PublicationStatus): string {
  return LABELS[status];
}

/** The tone of a status's badge and relevance meter: hidden has none. */
export function articleStatusTone(status: PublicationStatus): 'success' | 'warning' | null {
  return status === 'published' ? 'success' : status === 'needs_review' ? 'warning' : null;
}

/** A title as staff read it: "Untitled post" when the source gave none. */
export function titleLabel(title: string | null): string {
  return title ? title : 'Untitled post';
}

/** An organization as staff read it: "Unknown" when none was found. */
export function organizationLabel(organization: string | null): string {
  return organization ?? 'Unknown';
}

/** The URL when it is absolute `http` or `https`, otherwise null: a source link opens only then. */
export function safeHttpUrl(url: string): string | null {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:' ? url : null;
  } catch {
    return null;
  }
}

/** A count with thousands separators ("18,432"). */
export function formatCount(count: number): string {
  return counts.format(count);
}

/**
 * The range a page shows: "Showing 11–20 of 18,432 articles". An empty list, or a page past
 * the end, says how many there are.
 */
export function pageRange(page: number, pageSize: number, total: number): string {
  const noun = total === 1 ? 'article' : 'articles';
  if (total === 0) return 'Showing 0 articles';
  const first = (page - 1) * pageSize + 1;
  if (first > total) return `Showing 0 of ${formatCount(total)} ${noun}`;
  const last = Math.min(page * pageSize, total);
  return `Showing ${formatCount(first)}–${formatCount(last)} of ${formatCount(total)} ${noun}`;
}

/** The six categories' slugs, in the order of L2-015. */
export const ARTICLE_CATEGORIES = [
  'events',
  'races-challenges',
  'gyms-studios',
  'ministry-programs',
  'nutrition',
  'faith-wellness',
] as const;

const CATEGORY_NAMES: Record<string, string> = {
  events: 'Events',
  'races-challenges': 'Races & Challenges',
  'gyms-studios': 'Gyms & Studios',
  'ministry-programs': 'Ministry Programs',
  nutrition: 'Nutrition',
  'faith-wellness': 'Faith & Wellness',
};

/** A category's name; a slug Keryx does not know shows as itself. */
export function categoryName(slug: string): string {
  return Object.hasOwn(CATEGORY_NAMES, slug) ? CATEGORY_NAMES[slug] : slug;
}
