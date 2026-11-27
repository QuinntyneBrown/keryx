import { searchPause, skeletonDelay } from '../public-api';

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

describe('skeletonDelay', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  // L2-055 AC1: skeleton rows only when no answer has come within 300 ms.
  it('shows the skeleton 300 ms after a load starts, and not once the load ends first', () => {
    const shown: string[] = [];
    const slow = skeletonDelay();
    slow.run(() => shown.push('slow'));
    vi.advanceTimersByTime(299);
    expect(shown).toEqual([]);
    vi.advanceTimersByTime(1);
    expect(shown).toEqual(['slow']);

    const fast = skeletonDelay();
    fast.run(() => shown.push('fast'));
    vi.advanceTimersByTime(299);
    fast.cancel();
    vi.advanceTimersByTime(1000);
    expect(shown).toEqual(['slow']);
  });
});
