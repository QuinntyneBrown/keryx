/** An article's status: served, held for staff review, or hidden (L2-013). */
export type PublicationStatus = 'published' | 'needs_review' | 'hidden';

/** The fixed score below which a collected item is hidden. */
export const REVIEW_FLOOR = 0.45;

/**
 * The status a score gives under the publish threshold T, as the backend's PublicationPolicy
 * decides it: published at or above T, held from the review floor up to T, hidden below the
 * floor. With T below the floor nothing is held. Scores and thresholds have two decimals, so
 * they are compared in hundredths.
 */
export function publicationStatusFor(score: number, threshold: number): PublicationStatus {
  const hundredths = Math.round(score * 100);
  if (hundredths >= Math.round(threshold * 100)) {
    return 'published';
  }
  return hundredths >= Math.round(REVIEW_FLOOR * 100) ? 'needs_review' : 'hidden';
}
