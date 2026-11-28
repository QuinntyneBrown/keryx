import { NgTemplateOutlet } from '@angular/common';
import { booleanAttribute, Component, computed, input, output } from '@angular/core';
import { Icon } from '../icon/icon';
import { Skeleton } from '../skeleton/skeleton';

/**
 * One open issue (docs/design-system/components/issue-card.html): its severity as an icon and a
 * word a screen reader hears ("Error:"), its title, what it is about and when it occurred. Every
 * text is bound as text.
 */
@Component({
  selector: 'kx-issue-card',
  imports: [Icon, NgTemplateOutlet, Skeleton],
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

  /** Where the card leads: the run the issue belongs to (L2-045 AC3); none for a plain card. */
  readonly href = input<string | null>(null);
  /** A plain click on the card's link, for the app's router; other clicks are the browser's. */
  readonly navigate = output<string>();

  protected readonly icon = computed(() => (this.severity() === 'error' ? 'danger' : 'warning'));

  protected follow(event: MouseEvent, href: string): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
