import { Component, ElementRef, inject, input, model } from '@angular/core';
import { Icon } from '../icon/icon';
import { Input } from '../input/input';

/**
 * A search field (docs/design-system/components/search.html): a magnifier over a search input
 * with a visually hidden label; the placeholder lists what can be searched. `value` is two-way
 * and changes on every key, so the consumer decides when to search.
 */
@Component({
  selector: 'kx-search',
  imports: [Icon, Input],
  templateUrl: './search.html',
  styleUrl: './search.scss',
})
export class Search {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly controlId = input.required<string>();
  /** The visually hidden label: "Search articles". */
  readonly label = input.required<string>();
  readonly placeholder = input<string>();
  readonly value = model('');

  focus(): void {
    this.host.nativeElement.querySelector('input')?.focus();
  }
}
