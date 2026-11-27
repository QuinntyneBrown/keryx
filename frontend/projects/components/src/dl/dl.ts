import { NgTemplateOutlet } from '@angular/common';
import { Component, input, type TemplateRef } from '@angular/core';

/** A term of a `kx-dl`: its key names the template in `values` that fills its value. */
export interface DlItem {
  key: string;
  term: string;
}

/**
 * Terms and their values (docs/design-system/components/dl.html), each value announced with its
 * term. The list draws its own `dt` and `dd`, so its stylesheet reaches them; each value comes
 * from the consumer's template for that key, given in `values`, as a table's cells do.
 */
@Component({
  selector: 'kx-dl',
  imports: [NgTemplateOutlet],
  templateUrl: './dl.html',
  styleUrl: './dl.scss',
})
export class Dl {
  readonly items = input.required<readonly DlItem[]>();
  readonly values = input.required<Readonly<Record<string, TemplateRef<unknown>>>>();
}
