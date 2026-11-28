import { booleanAttribute, Component, input } from '@angular/core';

/**
 * A turning ring for work under way (docs/design-system/components/spinner.html). It is hidden
 * from assistive technology: the words beside it say what is happening.
 */
@Component({ selector: 'kx-spinner', templateUrl: './spinner.html', styleUrl: './spinner.scss' })
export class Spinner {
  /** The small size, beside a line of text in place of a small icon. */
  readonly small = input(false, { transform: booleanAttribute });
}
