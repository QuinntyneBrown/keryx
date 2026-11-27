import { Component, input, output } from '@angular/core';
import { Icon, type IconName } from '../icon/icon';
import { IconBtn } from '../icon-btn/icon-btn';

export type ToastTone = 'success' | 'warning' | 'danger' | 'info';

const ICONS: Record<ToastTone, IconName> = {
  success: 'success',
  warning: 'warning',
  danger: 'warning',
  info: 'info',
};

/**
 * A notification (docs/design-system/components/toast.html): an icon, a title, a sentence, at
 * most one action, and Dismiss. It sits in a `kx-toast-region`, which announces it politely.
 * Everything is rendered as text.
 */
@Component({
  selector: 'kx-toast',
  imports: [Icon, IconBtn],
  templateUrl: './toast.html',
  styleUrl: './toast.scss',
})
export class Toast {
  readonly tone = input<ToastTone>('info');
  readonly title = input.required<string>();
  readonly text = input<string>();
  /** The one action's label, such as "Undo". */
  readonly actionLabel = input<string>();
  /** The action was chosen. */
  readonly acted = output<void>();
  /** Dismiss was pressed. */
  readonly dismiss = output<void>();

  protected icon(): IconName {
    return ICONS[this.tone()];
  }
}
