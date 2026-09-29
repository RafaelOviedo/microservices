using AutoMapper;
using Order.Domain.Orders;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Application.Orders;

public sealed class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<OrderItem, OrderItemResponse>()
            .ForCtorParam(nameof(OrderItemResponse.Quantity), options => options.MapFrom(item => item.ConfirmedQuantity ?? item.Quantity))
            .ForCtorParam(nameof(OrderItemResponse.UnitPrice), options => options.MapFrom(item => item.UnitPrice.Amount))
            .ForCtorParam(nameof(OrderItemResponse.Subtotal), options => options.MapFrom(item => item.UnitPrice.Amount * (item.ConfirmedQuantity ?? item.Quantity)));
        CreateMap<OrderEntity, OrderResponse>()
            .ForCtorParam(nameof(OrderResponse.CustomerId), options => options.MapFrom(order => order.Customer.Id))
            .ForCtorParam(nameof(OrderResponse.CustomerName), options => options.MapFrom(order => order.Customer.Name))
            .ForCtorParam(nameof(OrderResponse.Status), options => options.MapFrom(order => order.Status.ToString()))
            .ForCtorParam(nameof(OrderResponse.Total), options => options.MapFrom(order => order.Total.Amount))
            .ForCtorParam(nameof(OrderResponse.Items), options => options.MapFrom(order => order.Items.Where(item => item.ConfirmedQuantity == null || item.ConfirmedQuantity > 0).OrderBy(item => item.ProductId)));
    }
}
