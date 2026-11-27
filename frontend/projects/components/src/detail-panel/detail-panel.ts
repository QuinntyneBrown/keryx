import {
  booleanAttribute,
  Component,
  ElementRef,
  inject,
  input,
  output,
  type AfterViewInit,
} from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';
import { Icon } from '../icon/icon';
import { IconBtn } from '../icon-btn/icon-btn';

/**
 * One record beside the list it came from (docs/design-system/components/detail-panel.html): an
 * `aside` named by the record's title, with its id, status, title, summary and "Open original",
 * then the facts and alerts the consumer projects. From 1024 px it sits beside the list; below,
 * it covers the screen. Close and Escape emit `closed`. Every text is rendered as text, and the
 * original opens only when the consumer gives an `http(s)` URL.
 */
@Component({
  selector: 'kx-detail-panel',
  imports: [Badge, Icon, IconBtn],
  templateUrl: './detail-panel.html',
  styleUrl: './detail-panel.scss',
  host: { '(keydown.escape)': 'closed.emit()' },
})
export class DetailPanel implements AfterViewInit {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

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

  ngAfterViewInit(): void {
    if (this.focusOnOpen()) this.focusTitle();
  }

  focusTitle(): void {
    this.host.nativeElement.querySelector<HTMLElement>('.detail-panel__title')?.focus();
  }
}
