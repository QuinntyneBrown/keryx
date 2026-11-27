import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { App, PageBody } from '../public-api';

@Component({
  imports: [App],
  template: `
    <kx-app [bare]="bare" [split]="split">
      <aside slot="sidebar" class="e2e-sidebar">Sidebar</aside>
      <p class="e2e-content">Content</p>
      <section slot="panel" class="e2e-panel">Panel</section>
    </kx-app>
  `,
})
class Host {
  bare = false;
  split = false;
}

function render(inputs: Partial<Host> = {}) {
  const fixture = TestBed.createComponent(Host);
  Object.assign(fixture.componentInstance, inputs);
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).querySelector('.app')!;
}

describe('App', () => {
  it('places the sidebar before the one main landmark, which can take focus', () => {
    const frame = render();

    expect(frame.getAttribute('class')).toBe('app');
    expect([...frame.children].map((child) => child.tagName)).toEqual(['ASIDE', 'MAIN']);
    const main = frame.querySelector('main')!;
    expect(main.id).toBe('main');
    expect(main.getAttribute('class')).toBe('app__main');
    expect(main.getAttribute('tabindex')).toBe('-1');
    expect(main.querySelector('.e2e-content')?.textContent).toBe('Content');
    // The panel belongs to the split arrangement only.
    expect(frame.querySelector('.e2e-panel')).toBeNull();
    expect(frame.querySelector('.app__content')).toBeNull();
  });

  it('is bare when there is nothing to navigate to', () => {
    expect(render({ bare: true }).getAttribute('class')).toBe('app app--bare');
  });

  it('holds the content and a panel side by side when split', () => {
    const main = render({ split: true }).querySelector('main')!;

    expect(main.getAttribute('class')).toBe('app__main app__main--split');
    expect([...main.children].map((child) => child.getAttribute('class'))).toEqual([
      'app__content',
      'e2e-panel',
    ]);
    expect(main.querySelector('.app__content .e2e-content')?.textContent).toBe('Content');
  });
});

describe('PageBody', () => {
  it.each([
    [{}, 'page-body'],
    [{ form: true }, 'page-body page-body--form'],
    [{ centered: true }, 'page-body page-body--centered'],
  ])('gives the page its margins for %o', (inputs, classes) => {
    const fixture = TestBed.createComponent(PageBody);
    for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
    fixture.detectChanges();

    const body = (fixture.nativeElement as HTMLElement).firstElementChild!;
    expect(body.getAttribute('class')).toBe(classes);
  });
});
