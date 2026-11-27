import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Table, type TableColumn } from '../public-api';

interface Row {
  id: string;
  name: string;
  count: number;
}

@Component({
  imports: [Table],
  template: `
    <kx-table
      caption="Things, newest first"
      [columns]="columns"
      [rows]="rows"
      [rowKey]="key"
      [cells]="{ name: nameCell, count: countCell }"
    />
    <ng-template #nameCell let-row>{{ row.name }}</ng-template>
    <ng-template #countCell let-row>{{ row.count }}</ng-template>
  `,
})
class Host {
  columns: TableColumn[] = [
    { key: 'name', label: 'Thing', primary: true },
    { key: 'count', label: 'Count', numeric: true, wide: true },
  ];
  rows: Row[] = [
    { id: 'a', name: 'First', count: 1 },
    { id: 'b', name: 'Second', count: 2 },
  ];
  key = (row: unknown) => (row as Row).id;
}

function render(inputs: Partial<Host> = {}) {
  const fixture = TestBed.createComponent(Host);
  Object.assign(fixture.componentInstance, inputs);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Table', () => {
  it('wraps a table captioned for assistive technology', () => {
    const host = render();

    const caption = host.querySelector('.table-wrap > table.table > caption');
    expect(caption?.textContent).toBe('Things, newest first');
    expect(caption?.className).toBe('visually-hidden');
  });

  it('heads each column and marks its role in its cells', () => {
    const host = render();

    const heads = [...host.querySelectorAll('thead th')];
    expect(heads.map((th) => th.textContent)).toEqual(['Thing', 'Count']);
    expect(heads.map((th) => th.getAttribute('scope'))).toEqual(['col', 'col']);
    expect(heads[1].className).toBe('num from-lg');
    const cells = [...host.querySelectorAll('tbody tr:first-child td')];
    expect(cells.map((td) => td.className)).toEqual(['table__primary', 'num from-lg']);
  });

  it('fills each cell from its column template, a row per item', () => {
    const host = render();

    const rows = [...host.querySelectorAll('tbody tr')];
    expect(rows.map((tr) => [...tr.querySelectorAll('td')].map((td) => td.textContent))).toEqual([
      ['First', '1'],
      ['Second', '2'],
    ]);
  });

  it('labels an actions column for assistive technology only', () => {
    const host = render({
      columns: [{ key: 'name', label: 'Actions', hiddenLabel: true, actions: true }],
    });

    const head = host.querySelector('thead th') as HTMLElement;
    expect(head.className).toBe('table__col-actions');
    expect(head.querySelector('.visually-hidden')?.textContent).toBe('Actions');
  });
});
