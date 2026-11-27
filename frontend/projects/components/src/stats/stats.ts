import { Component, input } from '@angular/core';

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
@Component({ selector: 'kx-stats', templateUrl: './stats.html', styleUrl: './stats.scss' })
export class Stats {
  readonly items = input.required<readonly StatItem[]>();
}
