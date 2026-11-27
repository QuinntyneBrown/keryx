import { publicationStatusFor } from '../public-api';

describe('publicationStatusFor', () => {
  // L2-013 AC1, AC2, AC5: the same inline case table as the backend's PublicationPolicyTests.
  // Threshold T, score, status; with T below the review floor 0.45 nothing is held.
  it.each([
    [0.6, 1.0, 'published'],
    [0.6, 0.6, 'published'],
    [0.6, 0.59, 'needs_review'],
    [0.6, 0.45, 'needs_review'],
    [0.6, 0.44, 'hidden'],
    [0.6, 0.0, 'hidden'],
    [0.4, 0.44, 'published'],
    [0.4, 0.4, 'published'],
    [0.4, 0.39, 'hidden'],
  ] as const)('with T = %d puts a score of %d in %s', (threshold, score, status) => {
    expect(publicationStatusFor(score, threshold)).toBe(status);
  });

  // Scores and thresholds are served to two decimals; arithmetic on them must not slip below one.
  it('compares to the hundredth, so 0.57 (56.99999999999999 hundredths in binary) meets a threshold of 0.57', () => {
    expect(publicationStatusFor(0.57, 0.57)).toBe('published');
  });
});
