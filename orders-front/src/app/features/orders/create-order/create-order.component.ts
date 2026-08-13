import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { OrdersService } from '../../../core/services/orders.service';

@Component({
  selector: 'app-create-order',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './create-order.component.html',
})
export class CreateOrderComponent {
  private readonly fb = inject(FormBuilder);
  private readonly ordersService = inject(OrdersService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly apiError = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    customerName: ['', [Validators.required]],
    sku: ['', [Validators.required]],
    quantity: [
      1,
      [
        Validators.required,
        Validators.min(1),
        Validators.pattern(/^[1-9]\d*$/),
      ],
    ],
  });

  protected submit(): void {
    this.apiError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { customerName, sku, quantity } = this.form.getRawValue();

    this.ordersService.createOrder({ customerName, sku, quantity }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigateByUrl('/pedidos');
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.apiError.set(
          err.error?.error ?? 'Ocurrió un error inesperado. Intenta de nuevo.',
        );
      },
    });
  }
}
