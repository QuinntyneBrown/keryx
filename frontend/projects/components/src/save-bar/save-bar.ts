import { booleanAttribute, Component, input } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * The bar fixed to the bottom of the viewport while a form has unsaved changes
 * (docs/design-system/components/save-bar.html): what is pending, then the projected actions
 * (`slot="actions"`). It is a region named by `label` while there is work to save; with no label
 * it is a status, as when it confirms a save.
 *
 * - `tone`: `danger` when the changes cannot be saved yet, `success` when they were saved; each
 *   with its icon, so colour is never the only sign.
 * - `live`: the text alone is announced as it changes, as "Saving your changes…" is.
 */
@Component({
  selector: 'kx-save-bar',
  imports: [Icon],
  templateUrl: './save-bar.html',
  styleUrl: './save-bar.scss',
})
export class SaveBar {
  readonly text = input.required<string>();
  readonly label = input<string>();
  readonly tone = input<'neutral' | 'danger' | 'success'>('neutral');
  readonly live = input(false, { transform: booleanAttribute });
}
