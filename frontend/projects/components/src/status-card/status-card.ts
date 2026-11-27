import { booleanAttribute, Component, computed, input } from '@angular/core';
import { Icon, type IconName } from '../icon/icon';
import { Progress } from '../progress/progress';
import { Skeleton } from '../skeleton/skeleton';
import { Spinner } from '../spinner/spinner';
import { Stats, type StatItem } from '../stats/stats';

export type StatusTone = 'success' | 'warning' | 'danger' | 'info';

/**
 * How the last or current run is going (docs/design-system/components/status-card.html): an icon
 * and a headline that says the outcome in words, a meta line with the run's id kept whole, and
 * the stats. The history, a caption with the content projected below it, comes last.
 */
@Component({
  selector: 'kx-status-card',
  imports: [Icon, Progress, Skeleton, Spinner, Stats],
  templateUrl: './status-card.html',
  styleUrl: './status-card.scss',
})
export class StatusCard {
  readonly eyebrow = input('');
  readonly headline = input('');
  readonly meta = input('');
  /** The run's id, shown whole in mono after the meta line. */
  readonly reference = input<string | null>(null);
  readonly tone = input<StatusTone>('info');
  /** The stats; while loading, their shapes, which the skeletons follow. */
  readonly stats = input<readonly StatItem[]>([]);
  /** What the history below the stats plots; with none, the history is not drawn. */
  readonly historyCaption = input<string | null>(null);
  /** How far a run in progress has got; with it, a spinner stands in for the icon. */
  readonly progress = input<{ label: string; value: number; max: number } | null>(null);
  /** The run is still being read: skeletons in the card's shape stand in (L2-046 AC1). */
  readonly loading = input(false, { transform: booleanAttribute });

  protected readonly iconClasses = computed(
    () => `status-card__icon status-card__icon--${this.tone()}`,
  );
  protected readonly icon = computed<IconName>(() => this.tone());
}
