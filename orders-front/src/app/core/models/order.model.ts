export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected';

export interface Order {
  id: string;
  customerName: string;
  sku: string;
  quantity: number;
  status: OrderStatus;
  createdAt: string;
}

export interface CreateOrderRequest {
  customerName: string;
  sku: string;
  quantity: number;
}

export interface ApiResponse<T> {
  success: boolean;
  code: number;
  message: string;
  error: string | null;
  data: T | null;
}
