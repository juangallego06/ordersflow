using OrdersApi.Api.Middlewares;
using OrdersApi.Application;
using OrdersApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();