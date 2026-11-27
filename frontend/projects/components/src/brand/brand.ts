import { NgTemplateOutlet } from '@angular/common';
import { booleanAttribute, Component, computed, input, output } from '@angular/core';

/** The lockup's ground. With none it is the sidebar form: the tile takes the shell's raised ink. */
export type BrandTone = 'on-ink' | 'on-light' | 'on-volt';

/** The six sizes of the mark, in pixels. With none the mark is the sidebar's 28 px. */
export type BrandSize = 16 | 24 | 32 | 40 | 64 | 96;

/**
 * The Keryx lockup: the mark beside the wordmark (docs/design-system/components/brand.html).
 *
 * With an `href` it is one link, named by `label`; a plain click emits `navigate` with the
 * `href` so the app follows it with its router, and any other click is left to the browser.
 * With no `href` it is one image named by `label`.
 */
@Component({
  selector: 'kx-brand',
  imports: [NgTemplateOutlet],
  templateUrl: './brand.html',
  styleUrl: './brand.scss',
})
export class Brand {
  /** Where the lockup leads. With none it is an image, not a link. */
  readonly href = input<string>();
  /** The one accessible name of the whole lockup. */
  readonly label = input('Keryx');
  readonly tone = input<BrandTone>();
  readonly size = input<BrandSize>();
  /** The mark above the wordmark. */
  readonly stacked = input(false, { transform: booleanAttribute });

  /** A plain click on the link: the `href` to follow without loading the document again. */
  readonly navigate = output<string>();

  protected readonly classes = computed(() => {
    const tone = this.tone();
    const size = this.size();
    return [
      'brand',
      tone ? `brand--${tone}` : '',
      size ? `brand--${size}` : '',
      this.stacked() ? 'brand--stacked' : '',
    ]
      .filter(Boolean)
      .join(' ');
  });

  protected follow(event: MouseEvent, href: string): void {
    // A new tab, a new window or a download is the browser's to do.
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
