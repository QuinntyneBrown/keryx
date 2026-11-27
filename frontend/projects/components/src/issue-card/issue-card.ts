import { Component, computed, input } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * One open issue (docs/design-system/components/issue-card.html): its severity as an icon and a
 * word a screen reader hears ("Error:"), its title, what it is about and when it occurred. Every
 * text is bound as text.
 */
@Component({
  selector: 'kx-issue-card',
  imports: [Icon],
  templateUrl: './issue-card.html',
  styleUrl: './issue-card.scss',
})
export class IssueCard {
  readonly severity = input.required<'error' | 'warning'>();
  /** The severity in words, read before the title ("Error:"). */
  readonly severityWord = input.required<string>();
  readonly title = input.required<string>();
  /** What the issue is about ("Redeemed Run Club Toronto"); none for a run-wide issue. */
  readonly source = input<string | null>(null);
  readonly meta = input.required<string>();

  protected readonly icon = computed(() => (this.severity() === 'error' ? 'danger' : 'warning'));
}
