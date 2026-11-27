import { booleanAttribute, Component, input } from '@angular/core';
import { Skeleton } from '../skeleton/skeleton';

/** One figure of a stats row: its label, value, optional unit ("/64") and sub-line ("in 2h 14m"). */
export interface StatItem {
  label: string;
  value: string;
  unit?: string;
  sub?: string;
  size?: 'lg' | 'sm';
}

/**
 * A row of figures as a description list (docs/design-system/components/stat.html): each stat's
 * label is its term, so a screen reader reads "New articles, 47". Three stay in one row.
 */
@Component({
  selector: 'kx-stats',
  imports: [Skeleton],
  templateUrl: './stats.html',
  styleUrl: './stats.scss',
})
export class Stats {
  readonly items = input.required<readonly StatItem[]>();
  /**
   * The figures are loading: each item is drawn as skeletons in its shape (its value's size, and
   * a sub-line when it has one), so nothing shifts when the figures arrive.
   */
  readonly loading = input(false, { transform: booleanAttribute });
}
