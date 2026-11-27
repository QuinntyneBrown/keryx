import { booleanAttribute, Component, computed, input, output } from '@angular/core';

@Component({ selector: 'kx-btn', templateUrl: './btn.html', styleUrl: './btn.scss' })
export class Btn {
  readonly variant = input<'default' | 'primary' | 'ghost' | 'danger' | 'danger-solid'>('default');
  readonly small = input(false, { transform: booleanAttribute });
  readonly block = input(false, { transform: booleanAttribute });
  readonly grow = input(false, { transform: booleanAttribute });
  readonly label = input<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly busy = input(false, { transform: booleanAttribute });
  readonly activated = output<void>();
  protected readonly classes = computed(() =>
    [
      'btn',
      this.variant() === 'default' ? '' : 'btn--' + this.variant(),
      this.small() && 'btn--sm',
      this.block() && 'btn--block',
      this.grow() && 'btn--grow',
      this.label() && 'btn--icon',
    ]
      .filter(Boolean)
      .join(' '),
  );
}
