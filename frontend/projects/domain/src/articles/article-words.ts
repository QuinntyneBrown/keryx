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
