import { cronLine, savedLine, thresholdHelp } from './settings-words';

describe('cronLine', () => {
  // The next slot is the server's rule (L2-005): the rows of AC2 and AC3 are those of
  // backend/tests/Keryx.Application.Tests/Collection/CollectionScheduleTests.cs, in Toronto time.
  const cases: [number, string, string][] = [
    [6, '2026-11-27T09:46:00-05:00', 'cron: 0 */6 * * * · next 12:00 PM'],
    [3, '2026-11-27T09:46:00-05:00', 'cron: 0 */3 * * * · next 12:00 PM'],
    [3, '2026-11-27T12:00:00-05:00', 'cron: 0 */3 * * * · next 3:00 PM'],
    [6, '2026-11-27T18:00:00-05:00', 'cron: 0 */6 * * * · next 12:00 AM'],
    [12, '2026-11-27T13:00:00-05:00', 'cron: 0 */12 * * * · next 12:00 AM'],
    [1, '2026-11-27T09:46:00-05:00', 'cron: 0 * * * * · next 10:00 AM'],
    [24, '2026-11-27T09:46:00-05:00', 'cron: 0 0 * * * · next 12:00 AM'],
    // Summer time: the wall clock of Toronto, not UTC, decides the slot.
    [6, '2026-07-01T05:59:00-04:00', 'cron: 0 */6 * * * · next 6:00 AM'],
  ];
  for (const [hours, now, line] of cases) {
    it(`reads "${line}" every ${hours} h at ${now}`, () => {
      expect(cronLine(hours, new Date(now), 'America/Toronto')).toBe(line);
    });
  }
});

describe('thresholdHelp', () => {
  const cases: [number, string][] = [
    [0.6, 'Scores from 0.45 to 0.60 wait in Needs review. Below 0.45 are hidden.'],
    [0.65, 'Scores from 0.45 to 0.65 wait in Needs review. Below 0.45 are hidden.'],
    [0.9, 'Scores from 0.45 to 0.90 wait in Needs review. Below 0.45 are hidden.'],
    // At or below the review floor nothing is held: what is not published is hidden.
    [0.45, 'None wait in Needs review. Below 0.45 are hidden.'],
    [0.3, 'None wait in Needs review. Below 0.30 are hidden.'],
  ];
  for (const [threshold, help] of cases) {
    it(`reads "${help}" at ${threshold}`, () => {
      expect(thresholdHelp(threshold)).toBe(help);
    });
  }
});

describe('savedLine', () => {
  const cases: [number, string, string][] = [
    [
      3,
      '2026-11-27T09:46:00-05:00',
      'Saved at 9:46 AM. Keryx now collects every 3 hours — the next run is at 12:00 PM.',
    ],
    [
      1,
      '2026-11-27T09:46:30-05:00',
      'Saved at 9:46 AM. Keryx now collects every hour — the next run is at 10:00 AM.',
    ],
    [
      24,
      '2026-11-27T21:05:00-05:00',
      'Saved at 9:05 PM. Keryx now collects every day — the next run is at 12:00 AM.',
    ],
  ];
  for (const [hours, now, line] of cases) {
    it(`reads "${line}"`, () => {
      expect(savedLine(hours, new Date(now), 'America/Toronto')).toBe(line);
    });
  }
});
