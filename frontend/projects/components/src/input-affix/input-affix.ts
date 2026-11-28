import { Component, input } from '@angular/core';

/**
 * An input with its unit after it (docs/design-system/components/input.html, "With affix"): the
 * projected `kx-input`, then the suffix ("seconds").
 */
@Component({
  selector: 'kx-input-affix',
  templateUrl: './input-affix.html',
  styleUrl: './input-affix.scss',
})
export class InputAffix {
  readonly suffix = input.required<string>();
}
