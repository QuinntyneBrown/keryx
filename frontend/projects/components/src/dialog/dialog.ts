import { booleanAttribute, Component, input, output } from '@angular/core';
import { Icon } from '../icon/icon';
import { IconBtn } from '../icon-btn/icon-btn';

/**
 * The dialog's panel (docs/design-system/components/dialog.html): a head with the title, the
 * description and Close, a body that scrolls, and a foot of actions. Centred from 640 px, a
 * bottom sheet below (L2-074). It is drawn inside a CDK dialog, which owns the modal role, the
 * focus trap, Escape and the backdrop; the app names the title and description ids in the
 * dialog's config so the CDK container is labelled by them.
 *
 * The body and the actions are a form: Enter in a field submits it, which emits `submitted`.
 */
@Component({
  selector: 'kx-dialog',
  imports: [Icon, IconBtn],
  templateUrl: './dialog.html',
  styleUrl: './dialog.scss',
})
export class Dialog {
  readonly title = input.required<string>();
  readonly description = input<string>();
  readonly titleId = input.required<string>();
  readonly descriptionId = input<string>();
  /** A narrower panel, for a confirmation. */
  readonly small = input(false, { transform: booleanAttribute });
  /** Close does nothing while a request is in flight. */
  readonly closeDisabled = input(false, { transform: booleanAttribute });
  /** Close was pressed. */
  readonly dismiss = output<void>();
  /** The form was submitted, by Enter in a field or a submit button. */
  readonly submitted = output<void>();

  protected submit(event: Event): void {
    event.preventDefault();
    this.submitted.emit();
  }
}
