import { NgTemplateOutlet } from '@angular/common';
import { Component, input, type TemplateRef } from '@angular/core';

/** A column of a `kx-table`. */
export interface TableColumn {
  /** Names the template in the table's `cells` that fills its cells. */
  key: string;
  label: string;
  /** The record's own column: its name, and on narrow screens what the hidden columns held. */
  primary?: boolean;
  /** Right-aligned, with tabular figures. */
  numeric?: boolean;
  /** Secondary text. */
  muted?: boolean;
  /** Shown only from the large breakpoint; the primary cell shows its value below that. */
  wide?: boolean;
  /** The fixed-width last column of row actions. */
  actions?: boolean;
  /** The heading is read by assistive technology only. */
  hiddenLabel?: boolean;
}

/**
 * A data table (docs/design-system/components/table.html): a hairline-edged wrapper that scrolls
 * sideways when it must, a visually hidden caption, an overline head and a row per item. It draws
 * the rows and cells itself, so its stylesheet reaches them; each cell's content comes from the
 * consumer's template for that column, given in `cells`. The server pages every table.
 */
@Component({
  selector: 'kx-table',
  imports: [NgTemplateOutlet],
  templateUrl: './table.html',
  styleUrl: './table.scss',
})
export class Table {
  /** Says what the rows are and their order: "Sources, failing first". */
  readonly caption = input.required<string>();
  readonly columns = input.required<readonly TableColumn[]>();
  readonly rows = input.required<readonly unknown[]>();
  /** A row's stable identity, so a changed page reuses the rows it can. */
  readonly rowKey = input.required<(row: unknown) => string>();

  /**
   * The template that fills each column's cells, by column key; each gets its row as `let-row`.
   * An input rather than a content query: a signal content query adds about 6.6 kB to the
   * initial bundle (ADR 0008).
   */
  readonly cells = input.required<Readonly<Record<string, TemplateRef<{ $implicit: unknown }>>>>();

  protected template(key: string): TemplateRef<{ $implicit: unknown }> | null {
    return this.cells()[key] ?? null;
  }

  protected headClass(column: TableColumn): string | null {
    return this.classes(column, false);
  }

  protected cellClass(column: TableColumn): string | null {
    return this.classes(column, true);
  }

  private classes(column: TableColumn, cell: boolean): string | null {
    const classes = [
      cell && column.primary ? 'table__primary' : '',
      cell && column.muted ? 'table__muted' : '',
      column.numeric ? 'num' : '',
      column.wide ? 'from-lg' : '',
      !cell && column.actions ? 'table__col-actions' : '',
    ].filter(Boolean);
    return classes.length ? classes.join(' ') : null;
  }
}
