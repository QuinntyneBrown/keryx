import { booleanAttribute, Component, computed, input } from '@angular/core';

export type BadgeTone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

/**
 * A short status label (docs/design-system/components/badge.html): its text is projected, so
 * colour is never the only carrier. `dot` leads a status with a dot in its tone; `count` is a
 * number beside a label, as in a tab.
 */
@Component({ selector: 'kx-badge', templateUrl: './badge.html', styleUrl: './badge.scss' })
export class Badge {
  readonly tone = input<BadgeTone>('neutral');
  readonly dot = input(false, { transform: booleanAttribute });
  readonly count = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() =>
    [
      'badge',
      this.tone() === 'neutral' ? '' : 'badge--' + this.tone(),
      this.dot() && 'badge--dot',
      this.count() && 'badge--count',
    ]
      .filter(Boolean)
      .join(' '),
  );
}
