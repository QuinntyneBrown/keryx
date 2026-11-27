import { booleanAttribute, Component, input, output } from '@angular/core';

/**
 * A button that is only an icon (docs/design-system/components/icon-btn.html). Its `label` is its
 * accessible name, since the icon says nothing to assistive technology.
 */
@Component({ selector: 'kx-icon-btn', templateUrl: './icon-btn.html', styleUrl: './icon-btn.scss' })
export class IconBtn {
  readonly label = input.required<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly activated = output<void>();
}
