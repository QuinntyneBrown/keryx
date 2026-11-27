import { Component, input } from '@angular/core';

@Component({ selector: 'kx-icon', templateUrl: './icon.html', styleUrl: './icon.scss' })
export class Icon {
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly name = input.required<'sign-in' | 'success' | 'warning' | 'info' | 'lock'>();
}
