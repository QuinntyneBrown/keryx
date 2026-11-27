import { booleanAttribute, Component, computed, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';

/**
 * Children one above another with an even gap (docs/design-system/components/stack.html):
 * `sm` 8 px, `md` 16 px, `lg` 24 px, `xl` 32 px. With `list` it is a `<ul>` and its projected
 * children are `<li>`, so a screen reader counts them.
 */
@Component({
  selector: 'kx-stack',
  imports: [NgTemplateOutlet],
  templateUrl: './stack.html',
  styleUrl: './stack.scss',
})
export class Stack {
  readonly gap = input<'sm' | 'md' | 'lg' | 'xl'>('md');
  readonly list = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() =>
    this.gap() === 'md' ? 'stack' : `stack stack--${this.gap()}`,
  );
}
