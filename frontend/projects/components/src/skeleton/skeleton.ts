import { Component, computed, input } from '@angular/core';

/**
 * A shape standing in for content while it loads (docs/design-system/components/skeleton.html).
 * It is empty and not announced: the loading region carries `aria-busy` and one status sentence.
 */
@Component({ selector: 'kx-skeleton', templateUrl: './skeleton.html', styleUrl: './skeleton.scss' })
export class Skeleton {
  readonly shape = input<'text' | 'title' | 'pill' | 'stat' | 'circle' | 'icon' | 'tile' | 'chart'>(
    'text',
  );
  /** A share of the line it sits on, for text of uneven length. */
  readonly width = input<20 | 30 | 40 | 60 | 80 | null>(null);

  protected readonly classes = computed(() => {
    const width = this.width();
    return `skeleton skeleton--${this.shape()}${width ? ` skeleton--w${width}` : ''}`;
  });
}
