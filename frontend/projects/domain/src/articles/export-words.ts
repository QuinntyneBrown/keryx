import { formatCount } from './article-words';

/** An export's notification: its tone and words. */
export interface ExportNotice {
  tone: 'info' | 'warning' | 'danger';
  title: string;
  text: string;
}

const TORONTO_DATE = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'America/Toronto',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** The export's file, named for today's date in Toronto: "articles-2026-11-27.csv" (L2-054 AC1). */
export function exportFileName(now: Date): string {
  return `articles-${TORONTO_DATE.format(now)}.csv`;
}

/** What "Export CSV" says as the export starts, with the rows the list counts (L2-054 AC1). */
export function exportStartedNotice(fileName: string, rows: number): ExportNotice {
  return {
    tone: 'info',
    title: 'Export started',
    text: `${fileName} will download when it’s ready — ${formatCount(rows)} ${rows === 1 ? 'row' : 'rows'}.`,
  };
}
