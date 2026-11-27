import { Component, computed, input } from '@angular/core';

/**
 * A score from 0 to 1 as a short bar and its number (docs/design-system/components/meter.html).
 * The bar is decorative; the number is the text, labelled by its column header or term. Its tone
 * follows the status the score gave, so it never carries the state alone.
 */
@Component({ selector: 'kx-meter', templateUrl: './meter.html', styleUrl: './meter.scss' })
export class Meter {
  /** From 0 to 1. */
  readonly value = input.required<number>();
  /** The number as shown: "0.94". */
  readonly text = input.required<string>();
  readonly tone = input<'success' | 'warning' | null>(null);

  protected readonly classes = computed(() => {
    const tone = this.tone();
    return tone ? `meter meter--${tone}` : 'meter';
  });
  /**
   * The fill's width in the track's 100 units. The track is an SVG so the width is an attribute:
   * no inline style, and no style binding, whose first use pulls Angular's styling instructions
   * into the initial bundle (5.2 kB) even from a lazy screen.
   */
  protected readonly fill = computed(() =>
    Math.round(Math.min(1, Math.max(0, this.value())) * 100),
  );
}
