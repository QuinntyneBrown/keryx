import { booleanAttribute, Component, computed, input } from '@angular/core';
import { Icon } from '../icon/icon';
import { Skeleton } from '../skeleton/skeleton';

/**
 * One open issue (docs/design-system/components/issue-card.html): its severity as an icon and a
 * word a screen reader hears ("Error:"), its title, what it is about and when it occurred. Every
 * text is bound as text.
 */
@Component({
  selector: 'kx-issue-card',
  imports: [Icon, Skeleton],
  templateUrl: './issue-card.html',
  styleUrl: './issue-card.scss',
})
export class IssueCard {
  readonly severity = input<'error' | 'warning'>('error');
  /** The severity in words, read before the title ("Error:"). */
  readonly severityWord = input('');
  readonly title = input('');
  /** What the issue is about ("Redeemed Run Club Toronto"); none for a run-wide issue. */
  readonly source = input<string | null>(null);
  readonly meta = input('');
  /** The issue is still being read: skeletons in the card's shape stand in (L2-046 AC1). */
  readonly loading = input(false, { transform: booleanAttribute });

  protected readonly icon = computed(() => (this.severity() === 'error' ? 'danger' : 'warning'));
}
