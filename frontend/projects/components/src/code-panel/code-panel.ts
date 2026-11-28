import { Component, input } from '@angular/core';

/** One block of a code panel: what a screen reader calls it, and its code as text. */
export interface CodeBlock {
  label: string;
  code: string;
}

/**
 * The code panel (docs/design-system/components/code-panel.html): ink in both themes, a head
 * with a title and a status, and one or two blocks of code, the second muted. Each block is
 * focusable so it can be scrolled by keyboard, and its code is bound as text.
 */
@Component({
  selector: 'kx-code-panel',
  templateUrl: './code-panel.html',
  styleUrl: './code-panel.scss',
})
export class CodePanel {
  readonly title = input.required<string>();
  readonly status = input('');
  readonly blocks = input.required<readonly CodeBlock[]>();
}
