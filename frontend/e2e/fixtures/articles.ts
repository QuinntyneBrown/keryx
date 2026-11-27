// Fixture data for the Articles screen (docs/mocks/pages/articles/): rows, pages, counts and the
// regions the screen names them with. The clock is pinned by each spec that reads an age.

/** The instant the Articles specs pin the browser's clock to. */
export const NOW = new Date('2026-11-27T15:00:00Z');

export const REGIONS = [
  { slug: 'toronto', name: 'Toronto', time_zone: 'America/Toronto' },
  { slug: 'mississauga', name: 'Mississauga', time_zone: 'America/Toronto' },
  { slug: 'gta', name: 'GTA', time_zone: 'America/Toronto' },
  { slug: 'global', name: 'Global', time_zone: 'Etc/UTC' },
];

export const COUNTS = { all: 18432, needs_review: 6, published: 17911, hidden: 515 };

/** An article row with the given fields; `n` makes its id and title distinct. */
export function articleRow(n: number, fields: Record<string, unknown> = {}) {
  return {
    id: `art_${String(n).padStart(12, '0')}`,
    title: `Article ${n}`,
    organization: 'Redeemed Run Club',
    category: 'events',
    region: 'toronto',
    relevance: 0.94,
    status: 'published',
    published_at: '2026-11-27T12:00:00Z',
    collected_at: '2026-11-27T13:00:00Z',
    ...fields,
  };
}

/** One page of rows numbered from `first`, as `GET /admin/v1/articles` serves it. */
export function articlePage(page: number, total: number, rows?: unknown[], pageSize = 10) {
  const first = (page - 1) * pageSize + 1;
  const count = Math.max(0, Math.min(pageSize, total - first + 1));
  return {
    items: rows ?? Array.from({ length: count }, (_, i) => articleRow(first + i)),
    total,
    page,
    page_size: pageSize,
  };
}

/** One article as `GET /admin/v1/articles/{id}` serves it: the row fields and its provenance. */
export function articleDetail(n: number, fields: Record<string, unknown> = {}) {
  return {
    ...articleRow(n),
    status_reason: null,
    threshold: 0.6,
    decided_by: 'system',
    summary: 'A city-wide step challenge across 40 churches raises funds for winter shelters.',
    url: 'https://news.example.com/youth-100km',
    source: { id: 'src_aaaaaaaaaaaa', name: 'Google News' },
    tags: ['challenge', 'youth'],
    provenance: [
      {
        source_id: 'src_aaaaaaaaaaaa',
        source_name: 'Google News',
        url: 'https://news.example.com/youth-100km',
        first_seen_at: '2026-11-27T11:03:00Z',
        last_seen_at: '2026-11-27T11:03:00Z',
      },
    ],
    ...fields,
  };
}
