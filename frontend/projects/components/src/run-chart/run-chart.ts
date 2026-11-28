import { Component, input, output } from '@angular/core';

/** One run's bar. */
export interface RunChartBar {
  id: string;
  status: 'success' | 'warning' | 'failed' | 'running';
  /** Its height, a whole number from 0 to 100, against the best run shown. */
  value: number;
  /** What a screen reader says: "Today 6:00 AM: 47 new". */
  label: string;
  /** Its tooltip: "Today 6:00 AM · 47 new". */
  title: string;
  /** Where the bar leads: its run on the Runs screen (L2-044 AC5); none when it leads nowhere. */
  href?: string | null;
}

/**
 * New articles per run as a row of bars, oldest first
 * (docs/design-system/components/run-chart.html). Each bar's words are its label, so its
 * outcome is never carried by colour alone; a failed run is also a stub.
 */
@Component({
  selector: 'kx-run-chart',
  templateUrl: './run-chart.html',
  styleUrl: './run-chart.scss',
})
export class RunChart {
  readonly bars = input.required<readonly RunChartBar[]>();
  /** A plain click on a bar's link, for the app's router; other clicks are the browser's. */
  readonly navigate = output<string>();

  protected follow(event: MouseEvent, href: string): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
