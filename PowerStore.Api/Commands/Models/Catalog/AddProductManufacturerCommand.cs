﻿using PowerStore.Api.DTOs.Catalog;
using MediatR;

namespace PowerStore.Api.Commands.Models.Catalog
{
    public class AddProductManufacturerCommand : IRequest<bool>
    {
        public ProductDto Product { get; set; }
        public ProductManufacturerDto Model { get; set; }
    }
}
