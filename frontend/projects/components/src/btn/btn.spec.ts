import { TestBed } from '@angular/core/testing';
import { Btn } from '../public-api';

function render(inputs: Record<string, unknown> = {}) {
  const fixture = TestBed.createComponent(Btn);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, control: (fixture.nativeElement as HTMLElement).querySelector('.btn')! };
}

describe('Btn', () => {
  it('is a button when it leads nowhere', () => {
    const { control } = render();

    expect(control.tagName).toBe('BUTTON');
    expect(control.getAttribute('type')).toBe('button');
  });

  it('is a link, not a button, when it has an href', () => {
    const { control } = render({ href: '/', variant: 'primary' });

    expect(control.tagName).toBe('A');
    expect(control.getAttribute('href')).toBe('/');
    expect(control.getAttribute('class')).toBe('btn btn--primary');
    expect(control.hasAttribute('role')).toBe(false);
  });

  it('emits navigate with its href on a plain click and keeps the document', () => {
    const { fixture, control } = render({ href: '/runs' });
    const followed: string[] = [];
    fixture.componentInstance.navigate.subscribe((href) => followed.push(href));
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });

    control.dispatchEvent(click);

    expect(followed).toEqual(['/runs']);
    expect(click.defaultPrevented).toBe(true);
  });

  it.each([
    ['a middle click', { button: 1 }],
    ['a click with Ctrl', { ctrlKey: true }],
    ['a click with Command', { metaKey: true }],
    ['a click with Shift', { shiftKey: true }],
    ['a click with Alt', { altKey: true }],
  ])('leaves %s to the browser', (_, init) => {
    const { fixture, control } = render({ href: '/runs' });
    const followed: string[] = [];
    fixture.componentInstance.navigate.subscribe((href) => followed.push(href));
    const click = new MouseEvent('click', { bubbles: true, cancelable: true, ...init });

    control.dispatchEvent(click);

    expect(followed).toEqual([]);
    expect(click.defaultPrevented).toBe(false);
  });
});
