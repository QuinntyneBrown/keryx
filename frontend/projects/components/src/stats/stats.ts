import { booleanAttribute, Component, computed, input } from '@angular/core';
import { Skeleton } from '../skeleton/skeleton';

/** One figure of a stats row: its label, value, optional unit ("/64") and sub-line ("in 2h 14m"). */
export interface StatItem {
  label: string;
  value: string;
  unit?: string;
  sub?: string;
  /** The value's size: large (the default), the base size, or small. */
  size?: 'lg' | 'md' | 'sm';
  /** A value that reports trouble, as failed sources, reads in the danger colour. */
  tone?: 'danger';
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
  /** Four figures: two rows of two below 640 px, one row of four from 640 px (L2-057 AC7). */
  readonly four = input(false, { transform: booleanAttribute });
  /** A rule above the figures, set apart from the card's head. */
  readonly ruled = input(false, { transform: booleanAttribute });

  // Attribute bindings: a class binding's first use pulls Angular's styling instructions into the
  // initial bundle.
  protected readonly classes = computed(() =>
    ['stats', this.four() && 'stats--4', this.ruled() && 'stats--ruled'].filter(Boolean).join(' '),
  );

  protected valueClasses(item: StatItem): string {
    const size = item.size ?? 'lg';
    return [
      'stat__value',
      size === 'md' ? '' : `stat__value--${size}`,
      item.tone ? `stat__value--${item.tone}` : '',
    ]
      .filter(Boolean)
      .join(' ');
  }
}
