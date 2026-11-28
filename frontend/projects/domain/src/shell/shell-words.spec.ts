import { failingSourcesLabel } from '../public-api';

describe('failingSourcesLabel', () => {
  // L2-042 AC3: the Runs badge is named by the count, in the singular for one.
  it.each([
    [2, '2 failing sources'],
    [1, '1 failing source'],
    [12, '12 failing sources'],
    [0, null],
  ])('names %i failing sources %p', (count, label) => {
    expect(failingSourcesLabel(count)).toBe(label);
  });
});
