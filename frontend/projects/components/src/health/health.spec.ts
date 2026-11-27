import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Health, type HealthTone } from '../public-api';

@Component({
  imports: [Health],
  template: `<kx-health [tone]="tone" [prefix]="prefix" [text]="text" />`,
})
class Host {
  tone: HealthTone = 'healthy';
  prefix: string | null = null;
  text = 'Healthy';
}

function render(inputs: Partial<Host>) {
  const fixture = TestBed.createComponent(Host);
  Object.assign(fixture.componentInstance, inputs);
  fixture.detectChanges();
  return fixture.nativeElement.querySelector('.health') as HTMLElement;
}

describe('Health', () => {
  it.each([
    ['healthy', 'health'],
    ['warning', 'health health--warning'],
    ['danger', 'health health--danger'],
    ['paused', 'health health--paused'],
  ] as const)('draws %s as "%s"', (tone, classes) => {
    expect(render({ tone }).className).toBe(classes);
  });

  it('puts the category before the condition in hidden text', () => {
    const health = render({ tone: 'danger', prefix: 'Failing: ', text: '404 Not Found' });

    expect(health.textContent).toBe('Failing: 404 Not Found');
    expect(health.querySelector('.visually-hidden')?.textContent).toBe('Failing: ');
  });

  it('renders markup in the text as text', () => {
    const health = render({ text: '<b>bold</b>' });

    expect(health.textContent).toBe('<b>bold</b>');
    expect(health.querySelector('b')).toBeNull();
    expect(health.querySelector('.visually-hidden')).toBeNull();
  });
});
