import { Component, input } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * The wrapper a value or a control sits in (docs/design-system/components/field.html): a label
 * above, the content, then help underneath. With `for` the label is a `label` for that control;
 * without it, the field shows a value that cannot be edited and the label is plain text.
 *
 * An `error` is shown under the content with the id `errorId`, which the control names as its
 * description. The optional marker arrives with the first form that uses it.
 */
@Component({
  selector: 'kx-field',
  imports: [Icon],
  templateUrl: './field.html',
  styleUrl: './field.scss',
})
export class Field {
  readonly label = input.required<string>();
  /** The id of the control the label names. */
  readonly for = input<string>();
  readonly help = input<string>();
  /** What is wrong with the entry, in a sentence that says how to fix it. */
  readonly error = input<string | null>();
  /** The id of the error, which the control's `aria-describedby` names. */
  readonly errorId = input<string>();
}
