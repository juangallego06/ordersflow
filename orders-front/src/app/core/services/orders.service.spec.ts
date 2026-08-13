import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { OrdersService } from './orders.service';
import { ApiResponse, Order } from '../models/order.model';
import { environment } from '../../../environments/environment';

describe('OrdersService', () => {
  let service: OrdersService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        OrdersService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(OrdersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetchOrders() debe guardar los pedidos recibidos en el signal orders', () => {
    const mockOrders: Order[] = [
      {
        id: '1',
        customerName: 'Juan',
        sku: 'ABC-01',
        quantity: 5,
        status: 'Pending',
        createdAt: '2026-08-12T21:14:46.4336793Z',
      },
    ];
    const mockResponse: ApiResponse<Order[]> = {
      success: true,
      code: 200,
      message: 'ok',
      error: null,
      data: mockOrders,
    };

    service.fetchOrders();

    const req = httpMock.expectOne(`${environment.apiUrl}/orders`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);

    expect(service.orders()).toEqual(mockOrders);
    expect(service.loading()).toBe(false);
  });
});
