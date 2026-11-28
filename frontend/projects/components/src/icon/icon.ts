import { Component, input } from '@angular/core';

/** The icons drawn so far; each is added from the design system's set when a screen needs it. */
export type IconName =
  | 'sign-in'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'lock'
  | 'settings'
  | 'plus'
  | 'rss'
  | 'globe'
  | 'instagram'
  | 'calendar'
  | 'search'
  | 'close'
  | 'key'
  | 'copy'
  | 'articles'
  | 'overview'
  | 'runs'
  | 'circle'
  | 'loader'
  | 'arrow-right'
  | 'inbox'
  | 'chevron-left'
  | 'chevron-right'
  | 'play'
  | 'external'
  | 'eye'
  | 'eye-off';

@Component({ selector: 'kx-icon', templateUrl: './icon.html', styleUrl: './icon.scss' })
export class Icon {
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly name = input.required<IconName>();
}
