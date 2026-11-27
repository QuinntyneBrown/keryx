import { sourceFormValidation, type SourceFormEntries } from '../public-api';

const URL_MESSAGE = 'Enter a full URL starting with https://, like https://lifepointfit.ca/events.';
const ORG_MESSAGE = 'Enter the organisation’s name as it publishes it.';
const REGION_MESSAGE = 'Choose the region the organisation is in.';

const valid: SourceFormEntries = {
  url: 'https://lifepointfit.ca/events',
  organisation: 'LifePoint Fitness Ministry',
  region: 'toronto',
};

describe('sourceFormValidation', () => {
  it('finds nothing wrong with valid entries', () => {
    expect(sourceFormValidation(valid)).toEqual({});
  });

  // The address rules of L2-001 and L2-024 AC5, as the API applies them.
  it.each([
    ['lifepointfit', URL_MESSAGE],
    ['', URL_MESSAGE],
    ['   ', URL_MESSAGE],
    ['ftp://lifepointfit.ca/feed', URL_MESSAGE],
    ['javascript:alert(1)', URL_MESSAGE],
    ['https://', URL_MESSAGE],
    ['https://user:secret@lifepointfit.ca/feed', URL_MESSAGE],
    ['https://user@lifepointfit.ca/feed', URL_MESSAGE],
    ['https://lifepointfit.ca:8443/feed', URL_MESSAGE],
    ['https://lifepointfit.ca/' + 'a'.repeat(2030), URL_MESSAGE],
    ['https://lifepointfit.ca/events', undefined],
    ['http://lifepointfit.ca/events', undefined],
    ['https://lifepointfit.ca:443/events', undefined],
    ['  https://lifepointfit.ca/events  ', undefined],
    ['https://lifepointfit.ca/' + 'a'.repeat(2000), undefined],
  ])('words the URL %s as %s', (url, message) => {
    expect(sourceFormValidation({ ...valid, url }).url).toBe(message);
  });

  it.each([
    ['', ORG_MESSAGE],
    ['   ', ORG_MESSAGE],
    ['x'.repeat(121), ORG_MESSAGE],
    ['x'.repeat(120), undefined],
    ['LifePoint', undefined],
  ])('words the organisation %s as %s', (organisation, message) => {
    expect(sourceFormValidation({ ...valid, organisation }).organisation).toBe(message);
  });

  it('asks for a region when none is chosen', () => {
    expect(sourceFormValidation({ ...valid, region: '' }).region).toBe(REGION_MESSAGE);
  });

  it('reports every field that is wrong, in the order of the form', () => {
    expect(Object.keys(sourceFormValidation({ url: 'x', organisation: '', region: '' }))).toEqual([
      'url',
      'organisation',
      'region',
    ]);
  });
});
