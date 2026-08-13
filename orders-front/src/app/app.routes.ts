import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'pedidos' },
  {
    path: 'crear',
    loadComponent: () =>
      import('./features/orders/create-order/create-order.component').then(
        (m) => m.CreateOrderComponent,
      ),
  },
  {
    path: 'pedidos',
    loadComponent: () =>
      import('./features/orders/orders-list/orders-list.component').then(
        (m) => m.OrdersListComponent,
      ),
  },
  { path: '**', redirectTo: 'pedidos' },
];
