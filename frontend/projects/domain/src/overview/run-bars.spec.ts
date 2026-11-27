import { runBars, type RunPoint } from '../public-api';

const ZONE = 'America/Toronto';
const NOW = new Date('2026-11-28T14:46:00Z');

const point = (id: string, started_at: string, outcome: RunPoint['outcome'], added: number) => ({
  id,
  started_at,
  outcome,
  added,
});

describe('runBars', () => {
  // L2-044 AC1, AC2: oldest first, height against the best of them, labelled by time and count.
  it('turns runs served newest first into bars oldest first', () => {
    const bars = runBars(
      [
        point('run_4', '2026-11-28T11:00:00Z', 'completed_with_issues', 47),
        point('run_3', '2026-11-27T19:37:00Z', 'succeeded', 8),
        point('run_2', '2026-11-27T17:00:00Z', 'failed', 0),
        point('run_1', '2026-11-25T11:00:00Z', 'succeeded', 19),
      ],
      NOW,
      ZONE,
    );

    expect(bars).toEqual([
      {
        id: 'run_1',
        status: 'success',
        value: 40,
        label: 'Wed, Nov 25 6:00 AM: 19 new',
        title: 'Wed, Nov 25 6:00 AM · 19 new',
      },
      {
        id: 'run_2',
        status: 'failed',
        value: 0,
        label: 'Yesterday 12:00 PM: failed',
        title: 'Yesterday 12:00 PM · failed',
      },
      {
        id: 'run_3',
        status: 'success',
        value: 17,
        label: 'Yesterday 2:37 PM: 8 new',
        title: 'Yesterday 2:37 PM · 8 new',
      },
      {
        id: 'run_4',
        status: 'warning',
        value: 100,
        label: 'Today 6:00 AM: 47 new',
        title: 'Today 6:00 AM · 47 new',
      },
    ]);
  });

  it.each([
    ['succeeded', 'success', '5 new'],
    ['completed_with_issues', 'warning', '5 new'],
    ['failed', 'failed', 'failed'],
    ['running', 'running', 'running'],
  ] as const)('a %s run is a %s bar reading "%s"', (outcome, status, words) => {
    const [bar] = runBars([point('run_1', '2026-11-28T11:00:00Z', outcome, 5)], NOW, ZONE);

    expect(bar.status).toBe(status);
    expect(bar.label).toBe(`Today 6:00 AM: ${words}`);
  });

  it('reads 1 new in the singular count it served', () => {
    const [bar] = runBars([point('run_1', '2026-11-28T11:00:00Z', 'succeeded', 1)], NOW, ZONE);

    expect(bar.label).toBe('Today 6:00 AM: 1 new');
  });

  it('rounds each height to the nearest whole percent', () => {
    const bars = runBars(
      [
        point('run_2', '2026-11-28T11:00:00Z', 'succeeded', 47),
        point('run_1', '2026-11-28T05:00:00Z', 'succeeded', 6),
      ],
      NOW,
      ZONE,
    );

    expect(bars.map((bar) => bar.value)).toEqual([13, 100]);
  });

  it('draws every bar at its floor when no run added anything', () => {
    const bars = runBars(
      [
        point('run_2', '2026-11-28T11:00:00Z', 'succeeded', 0),
        point('run_1', '2026-11-28T05:00:00Z', 'succeeded', 0),
      ],
      NOW,
      ZONE,
    );

    expect(bars.map((bar) => bar.value)).toEqual([0, 0]);
  });

  it('draws nothing for no runs', () => {
    expect(runBars([], NOW, ZONE)).toEqual([]);
  });
});
