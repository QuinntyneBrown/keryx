/**
 * The Runs badge's accessible name (L2-042 AC3): "2 failing sources", "1 failing source", or
 * null when none is failing and the link carries no badge.
 */
export function failingSourcesLabel(count: number): string | null {
  if (count <= 0) return null;
  return count === 1 ? '1 failing source' : `${count} failing sources`;
}
