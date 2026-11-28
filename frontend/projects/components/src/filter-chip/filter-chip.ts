import { Component, input, output } from '@angular/core';

/** One chip of a filter: what it filters to and its words; `pressed` when it is the one shown. */
export interface FilterChipOption {
  value: string;
  label: string;
  pressed: boolean;
}

/**
 * A row of toggle chips that narrows a list (docs/design-system/components/filter-chip.html): a
 * group of buttons, the one in force marked `aria-pressed="true"`. Pressing a chip emits its
 * value; the consumer decides what is pressed.
 */
@Component({
  selector: 'kx-filter-chip',
  templateUrl: './filter-chip.html',
  styleUrl: './filter-chip.scss',
})
export class FilterChip {
  /** The group's accessible name ("Filter by source type"). */
  readonly label = input.required<string>();
  readonly options = input.required<readonly FilterChipOption[]>();
  readonly chosen = output<string>();
}
