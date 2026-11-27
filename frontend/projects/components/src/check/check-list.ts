import { Component, input } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * A group of checks (docs/design-system/components/check.html): a fieldset named by its legend.
 * When the group is wrong as a whole, its error goes under the list and describes the fieldset.
 */
@Component({
  selector: 'kx-check-list',
  imports: [Icon],
  templateUrl: './check-list.html',
  styleUrl: './check-list.scss',
})
export class CheckList {
  readonly legend = input.required<string>();
  readonly error = input<string | null>();
  readonly errorId = input.required<string>();
}
