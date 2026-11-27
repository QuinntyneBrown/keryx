import { booleanAttribute, Component, computed, input } from '@angular/core';

/**
 * Main content beside a rail (docs/design-system/components/stack.html, "Rail"): one column
 * below 1024 px, the content and a 340 px rail side by side from 1024 px. Its two children are
 * projected in order. `start` puts the rail first, as Runs' history beside its detail.
 */
@Component({ selector: 'kx-rail', templateUrl: './rail.html', styleUrl: './rail.scss' })
export class Rail {
  readonly start = input(false, { transform: booleanAttribute });

  // An attribute binding: a class binding's first use pulls Angular's styling instructions into
  // the initial bundle.
  protected readonly classes = computed(() =>
    this.start() ? 'layout-rail layout-rail--start' : 'layout-rail',
  );
}
