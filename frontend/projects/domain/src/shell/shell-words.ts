/**
 * The Runs badge's accessible name (L2-042 AC3): "2 failing sources", "1 failing source", or
 * null when none is failing and the link carries no badge.
 */
export function failingSourcesLabel(count: number): string | null {
  if (count <= 0) return null;
  return count === 1 ? '1 failing source' : `${count} failing sources`;
}

/**
 * The schedule under the next collection (L2-042 AC1): "Every 6 hours, Toronto time", with
 * "Every hour" for a one-hour interval and the zone named by its city.
 */
export function collectionRecurrence(intervalHours: number, timeZone: string): string {
  const every = intervalHours === 1 ? 'Every hour' : `Every ${intervalHours} hours`;
  const city = (timeZone.split('/').pop() ?? timeZone).replaceAll('_', ' ');
  return `${every}, ${city} time`;
}
