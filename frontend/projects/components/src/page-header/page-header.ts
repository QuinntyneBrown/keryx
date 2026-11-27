import { Component, input } from '@angular/core';

/**
 * The top of every screen (docs/design-system/components/page-header.html): its name as the one
 * `h1`, a sentence of context, and the screen's main action in `[slot=actions]`.
 */
@Component({
  selector: 'kx-page-header',
  templateUrl: './page-header.html',
  styleUrl: './page-header.scss',
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
