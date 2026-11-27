import { Component, input } from '@angular/core';
import { Icon } from '../icon/icon';

@Component({
  selector: 'kx-alert',
  imports: [Icon],
  templateUrl: './alert.html',
  styleUrl: './alert.scss',
})
export class Alert {
  readonly tone = input<'info' | 'success' | 'warning' | 'danger' | 'neutral'>('info');
  /** Its first line, in bold; a short alert is its text alone. */
  readonly title = input<string>();
  readonly announcement = input<'alert' | 'status'>();
}
