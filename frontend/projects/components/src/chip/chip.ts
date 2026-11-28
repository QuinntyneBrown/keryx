import { booleanAttribute, Component, input, output } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * A list of short values (docs/design-system/components/chip.html): each value a chip with its
 * remove button named "Remove <value>", then the dashed "Add" chip. A value added but not yet
 * saved is marked new. Values render as text.
 */
@Component({
  selector: 'kx-chip',
  imports: [Icon],
  templateUrl: './chip.html',
  styleUrl: './chip.scss',
})
export class Chip {
  /** The list's accessible name ("Topics"). */
  readonly label = input.required<string>();
  readonly values = input.required<readonly string[]>();
  /** The values added since the last save. */
  readonly fresh = input<readonly string[]>([]);
  /** The Add chip's accessible name ("Add to Topics"). */
  readonly addLabel = input.required<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  /** A value's remove button was pressed: its index. */
  readonly removeValue = output<number>();
  readonly addValue = output<void>();
}
