/**
 * One endpoint of the consumer API with its example (L2-067 AC4): the request after the base
 * address and a sample `200 OK` body in the shape the API serves (L2-026 to L2-030).
 */
export interface ConsumerApiExample {
  id: string;
  method: 'GET';
  path: string;
  description: string;
  request: string;
  response: unknown;
}

const ORGANIZATION = { id: 'org_3f8t6w1c9d2k', name: 'Shepherd’s 5K' };
const ARTICLE_URL = 'https://shepherds5k.ca/news/2025-registration';

const ARTICLE = {
  id: 'art_18432',
  title: 'Shepherd’s 5K returns to Lakefront Promenade',
  organization: ORGANIZATION,
  category: 'races-challenges',
  region: 'mississauga',
  published_at: '2025-03-11T12:14:00Z',
  url: ARTICLE_URL,
  summary: 'Registration is open for the spring run, with a family mile before the 5K.',
};

const EVENT = {
  id: 'evt_7k2m9q4x8b1n',
  title: 'Shepherd’s 5K',
  starts_at: '2025-05-03T12:00:00Z',
  ends_at: '2025-05-03T15:00:00Z',
  time_zone: 'America/Toronto',
  place_name: 'Lakefront Promenade Park',
  region: 'mississauga',
  organization: ORGANIZATION,
  url: ARTICLE_URL,
  article_id: 'art_18432',
};

const ORG = {
  id: 'org_3f8t6w1c9d2k',
  name: 'Shepherd’s 5K',
  kind: 'ministry',
  region: 'mississauga',
  website_url: 'https://shepherds5k.ca',
  article_count: 14,
};

/**
 * Every endpoint of L2-026 to L2-030, in the order the screen lists them. The examples are
 * static: no real key, record or address of a person appears in them.
 */
export const CONSUMER_API_EXAMPLES: readonly ConsumerApiExample[] = [
  {
    id: 'articles',
    method: 'GET',
    path: '/v1/articles',
    description: 'Published articles, newest first. Filter by region, category, org, since.',
    request: 'articles?region=toronto&limit=2',
    response: {
      data: [
        ARTICLE,
        {
          id: 'art_18431',
          title: 'Grace Barbell Club opens a second gym in Etobicoke',
          organization: { id: 'org_8b4n2r7v5x1q', name: 'Grace Barbell Club' },
          category: 'gyms-studios',
          region: 'toronto',
          published_at: '2025-03-11T10:02:00Z',
          url: 'https://gracebarbell.ca/blog/etobicoke',
          summary: 'The club’s second location opens in April with weekday classes.',
        },
      ],
      next_cursor: 'eyJpZCI6MTg0MzB9',
    },
  },
  {
    id: 'article',
    method: 'GET',
    path: '/v1/articles/{id}',
    description: 'One article with summary, tags and canonical link.',
    request: 'articles/art_18432',
    response: {
      ...ARTICLE,
      tags: ['5k', 'charity run'],
      collected_at: '2025-03-11T13:00:00Z',
      event_id: 'evt_7k2m9q4x8b1n',
      sources: [{ name: 'Shepherd’s 5K news', url: ARTICLE_URL }],
    },
  },
  {
    id: 'events',
    method: 'GET',
    path: '/v1/events',
    description: 'Upcoming events, soonest first. Filter by region, org, from, to.',
    request: 'events?region=mississauga&limit=1',
    response: { data: [EVENT], next_cursor: 'eyJpZCI6IjIwMjUtMDUtMDMifQ' },
  },
  {
    id: 'event',
    method: 'GET',
    path: '/v1/events/{id}',
    description: 'One event with its time, zone, place and organization.',
    request: 'events/evt_7k2m9q4x8b1n',
    response: EVENT,
  },
  {
    id: 'organizations',
    method: 'GET',
    path: '/v1/organizations',
    description: 'Christian fitness organisations Keryx tracks.',
    request: 'organizations?region=mississauga&limit=1',
    response: { data: [ORG], next_cursor: null },
  },
  {
    id: 'organization',
    method: 'GET',
    path: '/v1/organizations/{id}',
    description: 'One organization with its kind, region and article count.',
    request: 'organizations/org_3f8t6w1c9d2k',
    response: ORG,
  },
  {
    id: 'sources',
    method: 'GET',
    path: '/v1/sources',
    description: 'The sources Keryx reads, for attribution.',
    request: 'sources?limit=1',
    response: {
      data: [
        {
          name: 'Shepherd’s 5K news',
          kind: 'rss',
          organization: ORGANIZATION,
          region: 'mississauga',
        },
      ],
      next_cursor: 'eyJpZCI6InNyY18wMiJ9',
    },
  },
  {
    id: 'categories',
    method: 'GET',
    path: '/v1/categories',
    description: 'Categories and article counts.',
    request: 'categories',
    response: {
      data: [
        { slug: 'races-challenges', name: 'Races & Challenges', article_count: 2140 },
        { slug: 'gyms-studios', name: 'Gyms & Studios', article_count: 1386 },
      ],
    },
  },
  {
    id: 'regions',
    method: 'GET',
    path: '/v1/regions',
    description: 'Regions, Toronto first, with their time zones and article counts.',
    request: 'regions',
    response: {
      data: [
        { slug: 'toronto', name: 'Toronto', time_zone: 'America/Toronto', article_count: 6204 },
        {
          slug: 'mississauga',
          name: 'Mississauga',
          time_zone: 'America/Toronto',
          article_count: 1877,
        },
      ],
    },
  },
  {
    id: 'status',
    method: 'GET',
    path: '/v1/status',
    description: 'Last run time and pipeline health, for consumers to show freshness.',
    request: 'status',
    response: {
      last_run_at: '2025-03-11T10:00:00Z',
      status: 'completed_with_issues',
      next_run_at: '2025-03-11T16:00:00Z',
      articles_total: 18432,
    },
  },
];
