import {
  afterNextRender,
  booleanAttribute,
  Component,
  ElementRef,
  inject,
  Injector,
  input,
  output,
} from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * A list of short values (docs/design-system/components/chip.html): each value a chip with its
 * remove button named "Remove <value>", then the dashed "Add" chip. A value added but not yet
 * saved is marked new. Values render as text. When a value's remove button is pressed and the
 * list no longer holds it, focus moves to the chip that took its place, or to Add after the last
 * (L2-072 AC2).
 */
@Component({
  selector: 'kx-chip',
  imports: [Icon],
  templateUrl: './chip.html',
  styleUrl: './chip.scss',
})
export class Chip {
  /** The list's accessible name ("Topics"). */
  readonly label = input.required<string>();
  readonly values = input.required<readonly string[]>();
  /** The values added since the last save. */
  readonly fresh = input<readonly string[]>([]);
  /** The Add chip's accessible name ("Add to Topics"). */
  readonly addLabel = input.required<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  /** A value's remove button was pressed: its index. */
  readonly removeValue = output<number>();
  readonly addValue = output<void>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected removeAt(index: number): void {
    this.removeValue.emit(index);
    afterNextRender(
      () => {
        const element = this.host.nativeElement;
        const next =
          element.querySelectorAll<HTMLElement>('.chip__remove')[index] ??
          element.querySelector<HTMLElement>('.chip--add');
        next?.focus();
      },
      { injector: this.injector },
    );
  }
}
