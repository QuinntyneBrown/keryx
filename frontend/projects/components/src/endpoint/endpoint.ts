import { Component, input } from '@angular/core';

/**
 * An endpoint of the consumer API (docs/design-system/components/endpoint.html): its method in
 * green, its path in mono and a muted description beneath. A list row holds it; the row is what
 * is chosen and marked current.
 */
@Component({
  selector: 'kx-endpoint',
  templateUrl: './endpoint.html',
  styleUrl: './endpoint.scss',
})
export class Endpoint {
  readonly method = input.required<string>();
  readonly path = input.required<string>();
  readonly description = input.required<string>();
}
