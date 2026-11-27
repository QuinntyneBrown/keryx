import { Component, ElementRef, inject, input, output } from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';

/** A tab of a `kx-tabs`: its id names the element (`tab-<id>`), which labels the panel. */
export interface TabItem {
  id: string;
  label: string;
  /** The count beside the label, as shown ("18,432"); part of the tab's name. */
  count?: string;
  countTone?: BadgeTone;
}

/**
 * Tabs with counts above one panel (docs/design-system/components/tabs.html): the WAI-ARIA tabs
 * pattern with automatic activation. Only the selected tab is a tab stop; the arrow keys select
 * the next or previous tab, wrapping, and Home and End the first and last. The consumer draws
 * the panel, with `role="tabpanel"`, the id given as `panelId` and `aria-labelledby` the
 * selected tab's element id.
 */
@Component({
  selector: 'kx-tabs',
  imports: [Badge],
  templateUrl: './tabs.html',
  styleUrl: './tabs.scss',
})
export class Tabs {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Names the tab list: "Article status". */
  readonly label = input.required<string>();
  readonly tabs = input.required<readonly TabItem[]>();
  readonly selected = input.required<string>();
  readonly panelId = input.required<string>();
  /**
   * A tab was chosen, by pointer or key: its id. It is emitted even for the tab `selected` names,
   * since keys pressed faster than the consumer updates `selected` may return to it.
   */
  readonly selectedChange = output<string>();

  protected choose(id: string): void {
    this.selectedChange.emit(id);
  }

  protected move(event: KeyboardEvent, index: number): void {
    const count = this.tabs().length;
    const target =
      event.key === 'ArrowRight'
        ? (index + 1) % count
        : event.key === 'ArrowLeft'
          ? (index - 1 + count) % count
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? count - 1
              : null;
    if (target === null) return;
    event.preventDefault();
    this.host.nativeElement.querySelectorAll<HTMLElement>('.tab')[target]?.focus();
    this.choose(this.tabs()[target].id);
  }
}
