import {
  booleanAttribute,
  Component,
  ElementRef,
  inject,
  input,
  output,
  type AfterViewInit,
  type OnChanges,
  type OnDestroy,
  type SimpleChanges,
} from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';
import { Btn } from '../btn/btn';
import { Icon } from '../icon/icon';
import { IconBtn } from '../icon-btn/icon-btn';

/** A one-step action of the detail panel's foot. */
export type DetailAction = 'publish' | 'hide';

/**
 * One record beside the list it came from (docs/design-system/components/detail-panel.html): an
 * `aside` named by the record's title, with its id, status, title, summary and "Open original",
 * then the facts and alerts the consumer projects. From 1024 px it sits beside the list; below,
 * it covers the screen. Close and Escape emit `closed`. Every text is rendered as text, and the
 * original opens only when the consumer gives an `http(s)` URL.
 */
@Component({
  selector: 'kx-detail-panel',
  imports: [Badge, Btn, Icon, IconBtn],
  templateUrl: './detail-panel.html',
  styleUrl: './detail-panel.scss',
  host: { '(keydown.escape)': 'closed.emit()' },
})
export class DetailPanel implements AfterViewInit, OnChanges, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private refocus: ReturnType<typeof setTimeout> | undefined;

  /** The record's id: "art_18429". */
  readonly reference = input.required<string>();
  readonly status = input.required<string>();
  readonly statusTone = input<BadgeTone>('neutral');
  readonly title = input.required<string>();
  readonly summary = input<string>();
  /** The original's `http(s)` address, or null when it has none that may be opened. */
  readonly url = input<string | null>(null);
  /** The Close button's name: "Close article details". */
  readonly closeLabel = input.required<string>();
  /** Move focus to the title once shown, as when a person opened the record. */
  readonly focusOnOpen = input(false, { transform: booleanAttribute });
  readonly closed = output<void>();
  /** The one-step actions in the foot, in order; the foot follows the record's status. */
  readonly actions = input<readonly DetailAction[]>([]);
  /** The action in flight: its button shows busy and both are inert until it ends. */
  readonly busy = input<DetailAction | null>(null);
  readonly publish = output<void>();
  readonly hide = output<void>();

  /**
   * When an action ends, the pressed button may be gone or have been disabled, dropping focus:
   * focus then moves to the remaining action, once the foot shows the next state.
   */
  ngOnChanges(changes: SimpleChanges): void {
    const busy = changes['busy'];
    if (!busy || busy.firstChange || busy.currentValue !== null) return;
    clearTimeout(this.refocus);
    this.refocus = setTimeout(() => {
      const active = document.activeElement;
      if (active && active !== document.body && active.isConnected) return;
      this.host.nativeElement.querySelector<HTMLElement>('.detail-panel__foot .btn')?.focus();
    });
  }

  ngOnDestroy(): void {
    clearTimeout(this.refocus);
  }

  ngAfterViewInit(): void {
    if (this.focusOnOpen()) this.focusTitle();
  }

  focusTitle(): void {
    this.host.nativeElement.querySelector<HTMLElement>('.detail-panel__title')?.focus();
  }
}
