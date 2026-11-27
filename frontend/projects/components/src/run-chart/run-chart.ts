import { Component, input } from '@angular/core';

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
}
