import { booleanAttribute, Component, computed, input, model } from '@angular/core';

/**
 * A value chosen on a scale (docs/design-system/components/range.html): a native
 * `input type="range"`, operable by arrow keys, and its value written beside it in an `output`.
 * `value` is two-way; the field's label names the control by `controlId`.
 */
@Component({ selector: 'kx-range', templateUrl: './range.html', styleUrl: './range.scss' })
export class Range {
  readonly controlId = input.required<string>();
  readonly min = input.required<number>();
  readonly max = input.required<number>();
  readonly step = input.required<number>();
  readonly value = model.required<number>();
  /** Digits after the point in the written value ("0.65"). */
  readonly decimals = input(0);
  /** The id of the text that describes it, such as the field's help. */
  readonly describedBy = input<string>();
  readonly disabled = input(false, { transform: booleanAttribute });

  protected readonly written = computed(() => this.value().toFixed(this.decimals()));

  protected update(event: Event): void {
    this.value.set(Number((event.target as HTMLInputElement).value));
  }
}
