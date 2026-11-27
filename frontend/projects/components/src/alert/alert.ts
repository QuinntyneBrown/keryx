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
  readonly title = input.required<string>();
  readonly announcement = input<'alert' | 'status'>();
}
