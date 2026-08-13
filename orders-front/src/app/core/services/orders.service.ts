import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, CreateOrderRequest, Order } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/orders`;

  readonly orders = signal<Order[]>([]);
  readonly loading = signal(false);

  fetchOrders(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<Order[]>>(this.baseUrl).subscribe({
      next: (response) => {
        this.orders.set(response.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  createOrder(payload: CreateOrderRequest): Observable<ApiResponse<Order>> {
    return this.http.post<ApiResponse<Order>>(this.baseUrl, payload).pipe(
      tap((response) => {
        if (response.data) {
          this.orders.update((current) => [response.data as Order, ...current]);
        }
      }),
    );
  }
}
