import { booleanAttribute, Component, input, output } from '@angular/core';

/**
 * A checkbox with its label and a description (docs/design-system/components/check.html). The
 * whole label is the target, at least 44px tall on touch devices; invalid is the checkbox's
 * `aria-invalid`.
 */
@Component({ selector: 'kx-check', templateUrl: './check.html', styleUrl: './check.scss' })
export class Check {
  readonly name = input.required<string>();
  readonly value = input.required<string>();
  /** The label, set in the mono face when it is a machine word such as a scope. */
  readonly label = input.required<string>();
  readonly mono = input(false, { transform: booleanAttribute });
  readonly description = input<string>();
  readonly checked = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly invalid = input(false, { transform: booleanAttribute });
  /** The checkbox was checked (true) or cleared (false). */
  readonly toggled = output<boolean>();

  protected change(event: Event): void {
    this.toggled.emit((event.target as HTMLInputElement).checked);
  }
}
