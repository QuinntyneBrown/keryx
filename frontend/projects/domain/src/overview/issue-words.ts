import { dayAndTime } from '../articles/article-words';

const TITLES: Record<string, string> = {
  source_not_found: 'Feed returned 404',
  source_timeout: 'Source timed out',
  source_blocked: 'Source blocked Keryx',
  source_invalid: 'Feed couldn’t be read',
  items_held: 'Items held for review',
  stage_failed: 'A stage failed',
  run_skipped: 'Scheduled run skipped',
  alert_not_sent: 'Alert e-mail not sent',
};

/** An open issue's title by its kind (Overview design); a kind Keryx does not know reads generally. */
export function issueTitle(kind: string): string {
  return Object.hasOwn(TITLES, kind) ? TITLES[kind] : 'Something needs attention';
}

/**
 * How an issue is named in its buttons: its title in quotes, and what it concerns when that is a
 * source ("“Source timed out” for Covenant Cycling Collective").
 */
export function issueName(kind: string, subject: string | null): string {
  return subject ? `“${issueTitle(kind)}” for ${subject}` : `“${issueTitle(kind)}”`;
}

/** The notice that an issue was muted (notifications/run-toast/success.html). */
export function issueMutedNotice(
  kind: string,
  subject: string | null,
): { title: string; text: string } {
  const what = `“${issueTitle(kind)}” no longer raises alerts`;
  return { title: 'Issue muted', text: subject ? `${what} for ${subject}.` : `${what}.` };
}

/** The word a screen reader hears before an issue, since its icon is not read. */
export function issueSeverityWord(severity: 'error' | 'warning'): string {
  return severity === 'error' ? 'Error:' : 'Warning:';
}

const DAY_MS = 24 * 60 * 60 * 1000;

/** How many calendar days before `now` a moment fell, in a time zone. */
function daysBefore(at: string, now: Date, timeZone: string): number {
  const key = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
  // Both dates parse as midnight UTC, so the difference is a whole number of days.
  return (Date.parse(key.format(now)) - Date.parse(key.format(new Date(at)))) / DAY_MS;
}

/**
 * When an issue occurred (L2-045 AC2): its time when it is new, "Today, 6:01 AM"; over a streak,
 * its length and when it began, "4 runs in a row · since Mon, 12:02 PM", by weekday alone up to
 * six days back; a week back shares today's weekday, so it reads in full.
 */
export function issueWhen(
  since: string,
  consecutiveRuns: number,
  now: Date,
  timeZone: string,
): string {
  const { day, time } = dayAndTime(since, now, timeZone);
  if (consecutiveRuns <= 1) return `${day}, ${time}`;
  const shown = daysBefore(since, now, timeZone) < 7 ? day.split(',')[0] : day;
  return `${consecutiveRuns} runs in a row · since ${shown}, ${time}`;
}
