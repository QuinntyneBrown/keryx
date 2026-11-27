import { Component, computed, input } from '@angular/core';

/**
 * How far a task has got, as a thin bar with an accessible value
 * (docs/design-system/components/progress.html): a `progressbar` named by `label`, with its
 * count as the value and its total as the maximum.
 */
@Component({ selector: 'kx-progress', templateUrl: './progress.html', styleUrl: './progress.scss' })
export class Progress {
  readonly label = input.required<string>();
  readonly value = input.required<number>();
  readonly max = input.required<number>();

  /** The fill in whole percent of the track; a share without a stylesheet rule draws no fill. */
  protected readonly share = computed(() => Math.round((this.value() / this.max()) * 100));
}
