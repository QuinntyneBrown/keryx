import { Component, computed, input } from '@angular/core';
import { Icon, type IconName } from '../icon/icon';
import { Stats, type StatItem } from '../stats/stats';

export type StatusTone = 'success' | 'warning' | 'danger' | 'info';

/**
 * How the last or current run is going (docs/design-system/components/status-card.html): an icon
 * and a headline that says the outcome in words, a meta line with the run's id kept whole, and
 * the stats. Content after the stats (progress, history) is projected below.
 */
@Component({
  selector: 'kx-status-card',
  imports: [Icon, Stats],
  templateUrl: './status-card.html',
  styleUrl: './status-card.scss',
})
export class StatusCard {
  readonly eyebrow = input.required<string>();
  readonly headline = input.required<string>();
  readonly meta = input.required<string>();
  /** The run's id, shown whole in mono after the meta line. */
  readonly reference = input<string | null>(null);
  readonly tone = input<StatusTone>('info');
  readonly stats = input<readonly StatItem[]>([]);

  protected readonly iconClasses = computed(
    () => `status-card__icon status-card__icon--${this.tone()}`,
  );
  protected readonly icon = computed<IconName>(() => this.tone());
}
