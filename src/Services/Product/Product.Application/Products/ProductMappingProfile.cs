using AutoMapper;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Application.Products;

public sealed class ProductMappingProfile : Profile
{
    public ProductMappingProfile()
    {
        CreateMap<ProductEntity, ProductResponse>()
            .ForCtorParam(nameof(ProductResponse.Price), options => options.MapFrom(product => product.Price.Amount));
    }
}
