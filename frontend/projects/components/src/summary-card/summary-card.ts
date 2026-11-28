import { booleanAttribute, Component, input } from '@angular/core';
import { Skeleton } from '../skeleton/skeleton';
import { Stats, type StatItem } from '../stats/stats';

/**
 * One identifying value and three numbers about it (docs/design-system/components/summary-card.html):
 * the lead, a caption over a mono value that truncates, beside its stats from 768 px. Content
 * projected into it, such as an alert when the figures failed, takes the place of both.
 */
@Component({
  selector: 'kx-summary-card',
  imports: [Skeleton, Stats],
  templateUrl: './summary-card.html',
  styleUrl: './summary-card.scss',
})
export class SummaryCard {
  /** The section's accessible name ("API usage"). */
  readonly label = input.required<string>();
  /** The caption over the lead value ("Base URL"). */
  readonly caption = input('');
  /** The value people copy; with none, the projected content stands in for lead and stats. */
  readonly value = input<string | null>(null);
  /** The stats; while loading, their shapes, which the skeletons follow. */
  readonly stats = input<readonly StatItem[]>([]);
  /** The figures are being read: skeletons in the card's shape stand in. */
  readonly loading = input(false, { transform: booleanAttribute });
}
