import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CreateOrderComponent } from './create-order.component';

describe('CreateOrderComponent', () => {
  let component: CreateOrderComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CreateOrderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    const fixture: ComponentFixture<CreateOrderComponent> =
      TestBed.createComponent(CreateOrderComponent);
    component = fixture.componentInstance;
  });

  it('el formulario es inválido si la cantidad es 0 o negativa, y válido si es positiva', () => {
    const form = (component as any).form;

    form.setValue({ customerName: 'Juan', sku: 'ABC-01', quantity: 0 });
    expect(form.invalid).toBe(true);

    form.controls.quantity.setValue(-5);
    expect(form.invalid).toBe(true);

    form.controls.quantity.setValue(3);
    expect(form.valid).toBe(true);
  });
});
