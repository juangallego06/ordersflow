using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrdersApi.Api.Common;
using OrdersApi.Application.Commands.Orders;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Queries.Orders;

namespace OrdersApi.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        var order = await _sender.Send(command);

        var response = ApiResponse<OrderResponse>.Ok(
            order,
            StatusCodes.Status201Created,
            "Pedido creado exitosamente.");

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _sender.Send(new GetOrdersQuery());

        var response = ApiResponse<IEnumerable<OrderResponse>>.Ok(
            orders, StatusCodes.Status200OK, "Pedidos obtenidos exitosamente.");

        return Ok(response);
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrderById(string orderId)
    {
        var order = await _sender.Send(new GetOrderByIdQuery { OrderId = orderId });

        if (order is null)
        {
            var notFound = ApiResponse<OrderResponse>.Fail(
                StatusCodes.Status404NotFound, "Pedido no encontrado.", $"No existe un pedido con id '{orderId}'.");
            return NotFound(notFound);
        }

        var response = ApiResponse<OrderResponse>.Ok(order, StatusCodes.Status200OK, "Pedido obtenido exitosamente.");
        return Ok(response);
    }
}
