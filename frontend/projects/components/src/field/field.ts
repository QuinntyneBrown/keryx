import { Component, input } from '@angular/core';

/**
 * The wrapper a value or a control sits in (docs/design-system/components/field.html): a label
 * above, the content, then help underneath. With `for` the label is a `label` for that control;
 * without it, the field shows a value that cannot be edited and the label is plain text.
 *
 * Journey 2 needs only the read-only form (Settings › Account); the error row and the optional
 * marker arrive with the first form that uses them.
 */
@Component({
  selector: 'kx-field',
  templateUrl: './field.html',
  styleUrl: './field.scss',
})
export class Field {
  readonly label = input.required<string>();
  /** The id of the control the label names. */
  readonly for = input<string>();
  readonly help = input<string>();
}
