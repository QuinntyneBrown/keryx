import { booleanAttribute, Component, input, output } from '@angular/core';
import { Btn } from '../btn/btn';
import { Icon } from '../icon/icon';

/**
 * The range and pager under a server-paged table (docs/design-system/components/pagination.html):
 * the range sentence is a polite status, and "Previous" and "Next" sit in a nav labelled
 * "Pagination". The consumer words the range and decides which way is open.
 */
@Component({
  selector: 'kx-pagination',
  imports: [Btn, Icon],
  templateUrl: './pagination.html',
  styleUrl: './pagination.scss',
})
export class Pagination {
  /** "Showing 1–10 of 18,432 articles". */
  readonly range = input.required<string>();
  readonly hasPrevious = input(false, { transform: booleanAttribute });
  readonly hasNext = input(false, { transform: booleanAttribute });
  /** "Previous" or "Next" was pressed: -1 or 1. */
  readonly step = output<-1 | 1>();
}
