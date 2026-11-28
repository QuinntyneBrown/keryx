import { booleanAttribute, Component, input, model } from '@angular/core';

/**
 * An on/off setting (docs/design-system/components/switch.html): a native checkbox with
 * `role="switch"` and its visible label. The thumb's position shows the state, not colour alone.
 * `checked` is two-way.
 */
@Component({ selector: 'kx-switch', templateUrl: './switch.html', styleUrl: './switch.scss' })
export class Switch {
  readonly label = input.required<string>();
  readonly checked = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });

  protected toggle(event: Event): void {
    this.checked.set((event.target as HTMLInputElement).checked);
  }
}
