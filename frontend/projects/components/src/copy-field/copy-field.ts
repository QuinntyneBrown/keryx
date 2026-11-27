import { Component, input, output } from '@angular/core';
import { Btn } from '../btn/btn';
import { Icon } from '../icon/icon';

/**
 * A value to copy and its "Copy" button (docs/design-system/components/copy-field.html). The
 * value selects whole on one click, so it can be copied by hand when the clipboard is refused;
 * the consumer does the copying when `copyRequested` is emitted.
 */
@Component({
  selector: 'kx-copy-field',
  imports: [Btn, Icon],
  templateUrl: './copy-field.html',
  styleUrl: './copy-field.scss',
})
export class CopyField {
  readonly value = input.required<string>();
  /** "Copy" was pressed. */
  readonly copyRequested = output<void>();
}
