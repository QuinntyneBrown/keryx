import { TestBed } from '@angular/core/testing';
import { Brand } from '../public-api';

function render(inputs: Record<string, unknown> = {}) {
  const fixture = TestBed.createComponent(Brand);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, lockup: (fixture.nativeElement as HTMLElement).querySelector('.brand')! };
}

describe('Brand', () => {
  it('draws the 40-unit mark: a tile of radius 10 and the four strokes', () => {
    const { lockup } = render();

    const mark = lockup.querySelector('svg.brand__mark')!;
    expect(mark.getAttribute('viewBox')).toBe('0 0 40 40');
    expect(mark.getAttribute('aria-hidden')).toBe('true');
    const tile = mark.querySelector('rect.brand__tile')!;
    expect([
      tile.getAttribute('width'),
      tile.getAttribute('height'),
      tile.getAttribute('rx'),
    ]).toEqual(['40', '40', '10']);
    const strokes = [...mark.querySelectorAll('.brand__strokes path')];
    expect(strokes.map((path) => path.getAttribute('d'))).toEqual([
      'M15 9V31',
      'M9 16H15',
      'M15 16L28.5 9',
      'M20 20L29 31',
    ]);
    expect(lockup.querySelector('.brand__word')?.textContent).toBe('keryx');
  });

  it('is one image named "Keryx" when it leads nowhere', () => {
    const { lockup } = render();

    expect(lockup.tagName).toBe('SPAN');
    expect(lockup.getAttribute('role')).toBe('img');
    expect(lockup.getAttribute('aria-label')).toBe('Keryx');
    // The name is said once: the wordmark is hidden from assistive technology.
    expect(lockup.querySelector('.brand__word')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('is one link with the name it is given when it has an href', () => {
    const { lockup } = render({ href: '/', label: 'Keryx, go to overview' });

    expect(lockup.tagName).toBe('A');
    expect(lockup.getAttribute('href')).toBe('/');
    expect(lockup.getAttribute('aria-label')).toBe('Keryx, go to overview');
    expect(lockup.hasAttribute('role')).toBe(false);
  });

  it.each([
    [{}, 'brand'],
    [{ tone: 'on-ink' }, 'brand brand--on-ink'],
    [{ tone: 'on-light' }, 'brand brand--on-light'],
    [{ tone: 'on-volt' }, 'brand brand--on-volt'],
    [{ size: 40 }, 'brand brand--40'],
    [{ tone: 'on-ink', size: 64, stacked: true }, 'brand brand--on-ink brand--64 brand--stacked'],
  ])('carries the lockup classes for %o', (inputs, classes) => {
    expect(render(inputs).lockup.getAttribute('class')).toBe(classes);
  });

  it('has no tile on volt: the strokes stand on the background', () => {
    const { lockup } = render({ tone: 'on-volt' });

    expect(lockup.querySelector('.brand__tile')).toBeNull();
    expect(lockup.querySelectorAll('.brand__strokes path').length).toBe(4);
  });

  it('emits navigate with its href on a plain click and keeps the document', () => {
    const { fixture, lockup } = render({ href: '/', label: 'Keryx, go to overview' });
    const followed: string[] = [];
    fixture.componentInstance.navigate.subscribe((href) => followed.push(href));
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });

    lockup.dispatchEvent(click);

    expect(followed).toEqual(['/']);
    expect(click.defaultPrevented).toBe(true);
  });

  it.each([
    ['a middle click', { button: 1 }],
    ['a click with Ctrl', { ctrlKey: true }],
    ['a click with Command', { metaKey: true }],
    ['a click with Shift', { shiftKey: true }],
    ['a click with Alt', { altKey: true }],
  ])('leaves %s to the browser', (_, init) => {
    const { fixture, lockup } = render({ href: '/', label: 'Keryx, go to overview' });
    const followed: string[] = [];
    fixture.componentInstance.navigate.subscribe((href) => followed.push(href));
    const click = new MouseEvent('click', { bubbles: true, cancelable: true, ...init });

    lockup.dispatchEvent(click);

    expect(followed).toEqual([]);
    expect(click.defaultPrevented).toBe(false);
  });
});
