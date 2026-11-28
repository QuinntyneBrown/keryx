import { booleanAttribute, Component, input } from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';
import { Skeleton } from '../skeleton/skeleton';

/**
 * A bordered surface with a head (docs/design-system/components/card.html): a level-2 heading,
 * an optional status badge beside it, a meta line that may lead with a reference in mono, and
 * actions (`[slot=actions]`) at the end of the head. Its body is projected below.
 */
@Component({
  selector: 'kx-card',
  imports: [Badge, Skeleton],
  templateUrl: './card.html',
  styleUrl: './card.scss',
})
export class Card {
  readonly heading = input('');
  readonly headingId = input<string>();
  readonly badge = input<{ text: string; tone: BadgeTone } | null>(null);
  /** An identifier set in mono at the start of the meta line ("run_2418"). */
  readonly reference = input<string | null>(null);
  readonly meta = input<string | null>(null);
  /** The card's content is loading: skeletons stand in for its heading and meta line. */
  readonly loading = input(false, { transform: booleanAttribute });
}
