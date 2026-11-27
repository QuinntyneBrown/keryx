import { Component, input } from '@angular/core';

export type BadgeTone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

/** A short status label (docs/design-system/components/badge.html): its text is projected, so colour is never the only carrier. */
@Component({ selector: 'kx-badge', templateUrl: './badge.html', styleUrl: './badge.scss' })
export class Badge {
  readonly tone = input<BadgeTone>('neutral');
}
