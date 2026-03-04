namespace SimpleeCommerceApp.Models
{
    public class CartModels
    {
        public sealed class CartDto
        {
            public Guid CartId { get; init; }
            public IReadOnlyList<CartItemDto> Items { get; init; } = Array.Empty<CartItemDto>();
            public int TotalItems { get; init; }
            public decimal SubTotal { get; init; }
        }

        public sealed class CartItemDto
        {
            public Guid ItemId { get; init; }
            public Guid ProductId { get; init; }
            public string ProductName { get; init; } = "";
            public decimal UnitPrice { get; init; }
            public int Quantity { get; init; }
            public decimal LineTotal => UnitPrice * Quantity;
        }
    }
}
