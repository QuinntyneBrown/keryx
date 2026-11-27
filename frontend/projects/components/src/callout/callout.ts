import { Component, input } from '@angular/core';

/**
 * A highlighted message that stays on the screen until it is no longer true
 * (docs/design-system/components/callout.html), announced politely when it appears. What it
 * holds, such as a copy field, is projected under its title.
 */
@Component({ selector: 'kx-callout', templateUrl: './callout.html', styleUrl: './callout.scss' })
export class Callout {
  readonly title = input.required<string>();
}
