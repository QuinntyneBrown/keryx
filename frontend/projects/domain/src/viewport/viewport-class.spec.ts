import { viewportClass } from '../public-api';

describe('viewportClass', () => {
  // L2-090: XS under 576 px, SM from 576 px, MD from 768 px, LG from 992 px, XL from 1200 px.
  // Each boundary is a row, with the pixel before it, and the widths the responsive sweep uses.
  it.each([
    [0, 'xs'],
    [360, 'xs'],
    [575, 'xs'],
    [575.99, 'xs'],
    [576, 'sm'],
    [640, 'sm'],
    [767, 'sm'],
    [768, 'md'],
    [991, 'md'],
    [992, 'lg'],
    [1024, 'lg'],
    [1199, 'lg'],
    [1200, 'xl'],
    [1280, 'xl'],
    [1920, 'xl'],
  ] as const)('puts a viewport %d px wide in %s', (width, expected) => {
    expect(viewportClass(width)).toBe(expected);
  });
});
