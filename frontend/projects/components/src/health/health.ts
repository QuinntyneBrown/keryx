import { Component, computed, input } from '@angular/core';

export type HealthTone = 'healthy' | 'warning' | 'danger' | 'paused';

/**
 * A source's health (docs/design-system/components/health.html): a decorative dot and the
 * condition in words, so colour never carries the state alone. When the text does not say the
 * category, a hidden prefix does ("Failing: 404 Not Found"). Everything is rendered as text.
 */
@Component({ selector: 'kx-health', templateUrl: './health.html', styleUrl: './health.scss' })
export class Health {
  readonly tone = input<HealthTone>('healthy');
  /** The category, read aloud before the text: "Failing: ". */
  readonly prefix = input<string | null>(null);
  /** The condition in the source's own terms: "Healthy", "404 Not Found". */
  readonly text = input.required<string>();

  protected readonly classes = computed(() =>
    this.tone() === 'healthy' ? 'health' : `health health--${this.tone()}`,
  );
}
