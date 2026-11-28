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
 * The next scheduled run after `now`: slots are aligned to midnight in `timeZone`, every
 * `intervalHours`, as the server's schedule decides (L2-005). Exact to the minute.
 */
function nextRun(intervalHours: number, now: Date, timeZone: string): Date {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    hour: 'numeric',
    minute: 'numeric',
  }).formatToParts(now);
  const part = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? 0);
  // Minutes into the day there; the seconds past the minute never change the minute shown.
  const intoDay = part('hour') * 60 + part('minute');
  const next = (Math.floor(intoDay / 60 / intervalHours) + 1) * intervalHours * 60;
  return new Date(now.getTime() + (next - intoDay) * 60_000);
}

/** The line under "Run every": the cron of the chosen interval and its next run (L2-070 AC2). */
export function cronLine(intervalHours: number, now: Date, timeZone: string): string {
  const at = formatClockTime(nextRun(intervalHours, now, timeZone), timeZone);
  return `cron: ${CRON[intervalHours]} · next ${at}`;
}

/**
 * The save bar's confirmation (L2-071 AC4): "Saved at 9:46 AM. Keryx now collects every 3 hours
 * — the next run is at 12:00 PM."
 */
export function savedLine(intervalHours: number, now: Date, timeZone: string): string {
  const every =
    intervalHours === 24 ? 'day' : intervalHours === 1 ? 'hour' : `${intervalHours} hours`;
  const next = formatClockTime(nextRun(intervalHours, now, timeZone), timeZone);
  return `Saved at ${formatClockTime(now, timeZone)}. Keryx now collects every ${every} — the next run is at ${next}.`;
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
