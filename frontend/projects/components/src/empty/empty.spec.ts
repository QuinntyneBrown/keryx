import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Empty, type EmptyTone } from '../public-api';

@Component({
  imports: [Empty],
  template: `
    <kx-empty
      [title]="title"
      [text]="text"
      [reference]="reference"
      [level]="level"
      [card]="card"
      [compact]="compact"
      [tone]="tone"
      [announce]="announce"
    >
      @if (withIcon) {
        <svg slot="icon" class="e2e-icon"></svg>
      }
      @if (withActions) {
        <button slot="actions" type="button">Try again</button>
      }
    </kx-empty>
  `,
})
class Host {
  title = 'Nothing has been collected yet';
  text?: string;
  reference?: string;
  level: 1 | 2 | 3 = 2;
  card = false;
  compact = false;
  tone: EmptyTone = 'neutral';
  announce = false;
  withIcon = false;
  withActions = false;
}

function render(inputs: Partial<Host> = {}) {
  const fixture = TestBed.createComponent(Host);
  Object.assign(fixture.componentInstance, inputs);
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).querySelector('.empty')!;
}

describe('Empty', () => {
  it('states what is missing in a heading and nothing else unless given', () => {
    const empty = render();

    expect(empty.getAttribute('class')).toBe('empty');
    expect(empty.querySelector('h2.empty__title')?.textContent).toBe(
      'Nothing has been collected yet',
    );
    expect(empty.querySelector('.empty__text')).toBeNull();
    expect(empty.querySelector('.empty__ref')).toBeNull();
    expect(empty.hasAttribute('role')).toBe(false);
  });

  it.each([
    [1, 'H1'],
    [2, 'H2'],
    [3, 'H3'],
  ] as const)('puts the title at heading level %i', (level, tag) => {
    expect(render({ level }).querySelector('.empty__title')?.tagName).toBe(tag);
  });

  it('says why and what to do, and quotes the reference as text', () => {
    const empty = render({
      text: 'Keryx couldn’t reach its API just now.',
      reference: 'req_8f2c41d0',
    });

    expect(empty.querySelector('p.empty__text')?.textContent).toBe(
      'Keryx couldn’t reach its API just now.',
    );
    expect(empty.querySelector('p.empty__ref')?.textContent).toBe('Reference req_8f2c41d0');
  });

  it.each([
    [{ card: true }, 'empty empty--card'],
    [{ card: true, compact: true }, 'empty empty--card empty--compact'],
    [{ card: true, tone: 'error' as const }, 'empty empty--card empty--error'],
    [{ compact: true, tone: 'success' as const }, 'empty empty--compact empty--success'],
  ])('carries the variant classes for %o', (inputs, classes) => {
    expect(render(inputs).getAttribute('class')).toBe(classes);
  });

  it('announces itself when it replaces content that failed to load', () => {
    expect(render({ tone: 'error', announce: true }).getAttribute('role')).toBe('alert');
  });

  it('holds the icon in its disc and the actions in their row', () => {
    const empty = render({ withIcon: true, withActions: true });

    expect(empty.querySelector('.empty__icon .e2e-icon')).not.toBeNull();
    expect(empty.querySelector('.empty__actions button')?.textContent).toBe('Try again');
    // Icon disc, then the title, then the actions: the order of the design system's anatomy.
    expect([...empty.children].map((child) => child.getAttribute('class'))).toEqual([
      'empty__icon',
      'empty__title',
      'empty__actions',
    ]);
  });

  it('renders text a source wrote as text, never as markup', () => {
    const empty = render({ title: '<b>bold</b>', text: '<img src=x onerror=alert(1)>' });

    expect(empty.querySelector('.empty__title')?.textContent).toBe('<b>bold</b>');
    expect(empty.querySelector('.empty__text')?.textContent).toBe('<img src=x onerror=alert(1)>');
    expect(empty.querySelector('b, img')).toBeNull();
  });
});
