/** The five width ranges a requirement is specified and tested at (L2-090). */
export type ViewportClass = 'xs' | 'sm' | 'md' | 'lg' | 'xl';

/** The width, in CSS pixels, each class starts at, widest first. XS is everything narrower. */
const STARTS: readonly (readonly [ViewportClass, number])[] = [
  ['xl', 1200],
  ['lg', 992],
  ['md', 768],
  ['sm', 576],
];

/**
 * The viewport class of a width in CSS pixels: XS under 576, SM from 576, MD from 768, LG from
 * 992 and XL from 1200. These are the classes the requirements are checked at; the layout itself
 * changes at the design system's switch points, in CSS.
 */
export function viewportClass(width: number): ViewportClass {
  return STARTS.find(([, start]) => width >= start)?.[0] ?? 'xs';
}
