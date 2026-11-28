import { REVIEW_FLOOR, type PublicationStatus } from './publication-status';

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
export function pageRange(
  page: number,
  pageSize: number,
  total: number,
  [one, many]: readonly [string, string] = ['article', 'articles'],
): string {
  const noun = total === 1 ? one : many;
  if (total === 0) return `Showing 0 ${many}`;
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

/** The no-results state: the search as typed, or the filters when there is no search. */
export function noResultsWords(
  q: string,
  total: number,
): { title: string; text: string; action: string } {
  const all = `all ${formatCount(total)} ${total === 1 ? 'article' : 'articles'}`;
  const term = q.trim();
  return term
    ? {
        title: `No articles match “${term}”`,
        text: `Try a different word, or clear the search to see ${all}.`,
        action: 'Clear search',
      }
    : {
        title: 'No articles match these filters',
        text: `Try another region or category, or clear the filters to see ${all}.`,
        action: 'Clear filters',
      };
}

const DAY_KEY = (zone: string) =>
  new Intl.DateTimeFormat('en-CA', {
    timeZone: zone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });

/**
 * When an article was collected, in the staff member's time zone: "Today, 6:03 AM",
 * "Yesterday, 6:01 PM", otherwise "Mon, Mar 9, 6:02 PM", with the year when it is not this one.
 */
export function formatCollectedAt(at: string | Date, now: Date, timeZone: string): string {
  const { day, time } = dayAndTime(at, now, timeZone);
  return `${day}, ${time}`;
}

/**
 * The day and the time of day of a moment, in a time zone, as Keryx says them: day "Today",
 * "Yesterday", otherwise "Mon, Mar 9", with the year when it is not this one; time "6:03 AM".
 */
export function dayAndTime(
  at: string | Date,
  now: Date,
  timeZone: string,
): { day: string; time: string } {
  const when = new Date(at);
  const days = DAY_KEY(timeZone);
  const time = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hour: 'numeric',
    minute: '2-digit',
  }).format(when);
  const day = days.format(when);
  const today = days.format(now);
  if (day === today) return { day: 'Today', time };
  const yesterday = days.format(new Date(Date.parse(`${today}T12:00:00Z`) - 24 * 60 * 60 * 1000));
  if (day === yesterday) return { day: 'Yesterday', time };
  const sameYear = day.slice(0, 4) === today.slice(0, 4);
  const date = new Intl.DateTimeFormat('en-US', {
    timeZone,
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    ...(sameYear ? {} : { year: 'numeric' }),
  }).format(when);
  return { day: date, time };
}

/**
 * Why the system gave an article its status, as the panel's alert says it (L2-051 AC4): held
 * below the publish threshold in force when it was collected, or hidden below the review floor.
 */
export function articleReasonAlert(
  status: PublicationStatus,
  reason: string | null,
  threshold: number,
): { tone: 'warning' | 'neutral'; text: string } | null {
  if (status === 'needs_review' && reason === 'below_publish_threshold') {
    return {
      tone: 'warning',
      text: `Held because its relevance score is below the ${threshold.toFixed(2)} publish threshold.`,
    };
  }
  if (status === 'hidden' && reason === 'below_review_floor') {
    return {
      tone: 'neutral',
      text: `Hidden automatically: its relevance score is below ${REVIEW_FLOOR.toFixed(2)}. The API does not serve it.`,
    };
  }
  return null;
}
