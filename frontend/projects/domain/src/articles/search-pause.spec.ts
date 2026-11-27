import { searchPause } from '../public-api';

describe('searchPause', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  // L2-050 AC1: the search is sent once typing pauses for 300 ms; a key within the pause
  // cancels the pending one.
  it('runs only the last search, 300 ms after the last key', () => {
    const pause = searchPause();
    const sent: string[] = [];

    pause.run(() => sent.push('pick'));
    vi.advanceTimersByTime(299);
    pause.run(() => sent.push('pickleball'));
    vi.advanceTimersByTime(299);
    expect(sent).toEqual([]);

    vi.advanceTimersByTime(1);
    expect(sent).toEqual(['pickleball']);
  });

  it('sends nothing once cancelled, as when the screen closes', () => {
    const pause = searchPause();
    const sent: string[] = [];

    pause.run(() => sent.push('run'));
    pause.cancel();
    vi.advanceTimersByTime(1000);

    expect(sent).toEqual([]);
  });
});
