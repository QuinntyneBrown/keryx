import { booleanAttribute, Component, computed, input } from '@angular/core';

/**
 * The body of a page inside the frame (docs/design-system/components/app.html, "Page body"):
 * the layout margin at the sides and room below.
 *
 * - `form`: no wider than a form reads well, with room for the save bar below.
 * - `centered`: one centred card, the arrangement of a system page.
 */
@Component({
  selector: 'kx-page-body',
  templateUrl: './page-body.html',
  styleUrl: './page-body.scss',
})
export class PageBody {
  readonly form = input(false, { transform: booleanAttribute });
  readonly centered = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() =>
    [
      'page-body',
      this.form() ? 'page-body--form' : '',
      this.centered() ? 'page-body--centered' : '',
    ]
      .filter(Boolean)
      .join(' '),
  );
}
