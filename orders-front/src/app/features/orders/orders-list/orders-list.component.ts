import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { interval, startWith } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OrdersService } from '../../../core/services/orders.service';
import { OrderStatus } from '../../../core/models/order.model';

const POLLING_INTERVAL_MS = 10000;

@Component({
  selector: 'app-orders-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './orders-list.component.html',
})
export class OrdersListComponent {
  protected readonly ordersService = inject(OrdersService);

  protected readonly sortedOrders = computed(() =>
    [...this.ordersService.orders()].sort(
      (a, b) =>
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    ),
  );

  constructor() {
    interval(POLLING_INTERVAL_MS)
      .pipe(startWith(0), takeUntilDestroyed())
      .subscribe(() => this.ordersService.fetchOrders());
  }

  protected statusBadgeClass(status: OrderStatus): string {
    switch (status) {
      case 'Confirmed':
        return 'badge-success';
      case 'Rejected':
        return 'badge-error';
      default:
        return 'badge-warning';
    }
  }
}
