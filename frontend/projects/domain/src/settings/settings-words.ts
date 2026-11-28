import { REVIEW_FLOOR } from '../articles/publication-status';
import { formatClockTime } from '../overview/run-words';

/** The cron expression of each interval staff may choose (L2-005, L2-070 AC2). */
const CRON: Readonly<Record<number, string>> = {
  1: '0 * * * *',
  3: '0 */3 * * *',
  6: '0 */6 * * *',
  12: '0 */12 * * *',
  24: '0 0 * * *',
};

/**
 * The line under "Run every": the cron of the chosen interval and its next run ("next 12:00 PM").
 * Slots are aligned to midnight in `timeZone`, and the next is the first
 * after `now`, as the server's schedule decides (L2-005).
 */
export function cronLine(intervalHours: number, now: Date, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    hour: 'numeric',
    minute: 'numeric',
  }).formatToParts(now);
  const part = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? 0);
  // Minutes into the Toronto day; the seconds past the minute never change the minute shown.
  const intoDay = part('hour') * 60 + part('minute');
  const next = (Math.floor(intoDay / 60 / intervalHours) + 1) * intervalHours * 60;
  const at = new Date(now.getTime() + (next - intoDay) * 60_000);
  return `cron: ${CRON[intervalHours]} · next ${formatClockTime(at, timeZone)}`;
}

/**
 * The help under the publish threshold (L2-070 AC3): what waits for review and what is hidden.
 * Below the review floor an item is hidden; from the floor up to the threshold it waits.
 */
export function thresholdHelp(threshold: number): string {
  const t = threshold.toFixed(2);
  if (Math.round(threshold * 100) <= Math.round(REVIEW_FLOOR * 100)) {
    return `None wait in Needs review. Below ${t} are hidden.`;
  }
  const floor = REVIEW_FLOOR.toFixed(2);
  return `Scores from ${floor} to ${t} wait in Needs review. Below ${floor} are hidden.`;
}
