import { exampleRequest } from '../public-api';

const KEY = '  -H "Authorization: Bearer kx_live_…"';

describe('exampleRequest', () => {
  it.each([
    ['https://api.keryx.ca/v1', 'status', 'curl https://api.keryx.ca/v1/status'],
    ['https://api.keryx.ca/v1/', 'status', 'curl https://api.keryx.ca/v1/status'],
    [
      'https://api.keryx.ca/v1',
      'articles?region=toronto&limit=2',
      'curl https://api.keryx.ca/v1/articles?region=toronto&limit=2',
    ],
    ['https://staging.keryx.test/v1', 'regions', 'curl https://staging.keryx.test/v1/regions'],
    [null, 'status', 'curl /v1/status'],
  ])('%s and %s read “%s”', (baseUrl, request, line) => {
    expect(exampleRequest(baseUrl, request)).toBe(`${line} \\\n${KEY}`);
  });

  it('never carries more of a key than its placeholder', () => {
    expect(exampleRequest('https://api.keryx.ca/v1', 'status')).not.toMatch(/kx_live_[^…]/);
  });
});
