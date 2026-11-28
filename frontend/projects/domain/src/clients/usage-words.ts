import { formatClockTime } from '../overview/run-words';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;

/**
 * An error rate from 0 to 1 as a percentage (docs/detailed-designs/consumer-api/report-api-usage):
 * "—" with no requests; below 1% up to two decimals and from 1% up to one, trailing zeros dropped
 * ("0.4%", "12.5%", "3%"); a rate above 0 that would round to "0%" reads "<0.01%".
 */
export function formatErrorRate(rate: number, requests: number): string {
  if (requests === 0) return '—';
  const percent = rate * 100;
  if (percent === 0) return '0%';
  const decimals = percent < 1 ? 2 : 1;
  const rounded = Number(percent.toFixed(decimals));
  return rounded === 0 ? '<0.01%' : `${rounded}%`;
}

/**
 * When a client last called the API, read in the given zone: "just now" under a minute, "2 min
 * ago" under an hour, the time on the same day ("6:15 AM"), otherwise the date ("Feb 2", with the
 * year when it is not this year's), and "never" for a client that has not called.
 */
export function formatLastCall(at: string | null, now: Date, timeZone: string): string {
  if (at === null) return 'never';
  const when = new Date(at);
  const elapsed = Math.max(0, now.getTime() - when.getTime());
  if (elapsed < MINUTE) return 'just now';
  if (elapsed < HOUR) return `${Math.floor(elapsed / MINUTE)} min ago`;
  const day = (date: Date) => new Intl.DateTimeFormat('en-CA', { timeZone }).format(date);
  if (day(when) === day(now)) return formatClockTime(when, timeZone);
  const year = (date: Date) =>
    new Intl.DateTimeFormat('en-US', { timeZone, year: 'numeric' }).format(date);
  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    month: 'short',
    day: 'numeric',
    ...(year(when) === year(now) ? {} : { year: 'numeric' }),
  }).format(when);
}

/** The API usage's p95 latency as a stat's value and unit: "84" and "ms", or "—" with none. */
export function formatLatency(p95: number | null): { value: string; unit?: string } {
  return p95 === null
    ? { value: '—' }
    : { value: new Intl.NumberFormat('en-CA').format(p95), unit: 'ms' };
}

/** "48,210 requests", or "1 request". */
export function requestsLabel(requests: number): string {
  return `${new Intl.NumberFormat('en-CA').format(requests)} ${requests === 1 ? 'request' : 'requests'}`;
}
