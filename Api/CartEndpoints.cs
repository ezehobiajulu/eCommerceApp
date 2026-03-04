using SimpleeCommerceApp.Data.Repositories;
using System.Security.Claims;

namespace SimpleeCommerceApp.Api
{
    public static class CartEndpoints
    {
        public sealed record AddItemRequest(Guid ProductId, int Quantity);
        public sealed record UpdateQtyRequest(int Quantity);

        public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/cart").RequireAuthorization();
            group.MapGet("/", async (ClaimsPrincipal user, ICartRepository carts, CancellationToken ct) =>
            {
                var userId = GetUserId(user);
                var cart = await carts.GetOrCreateActiveCartAsync(userId, ct);
                return Results.Ok(cart);
            });

            group.MapPost("/items", async (ClaimsPrincipal user, AddItemRequest req, ICartRepository carts, CancellationToken ct) =>
            {
                var userId = GetUserId(user);
                await carts.AddItemAsync(userId, req.ProductId, req.Quantity, ct);
                var cart = await carts.GetOrCreateActiveCartAsync(userId, ct);
                return Results.Ok(cart);
            });

            group.MapPut("/items/{itemId:guid}", async (ClaimsPrincipal user, Guid itemId, UpdateQtyRequest req, ICartRepository carts, CancellationToken ct) =>
            {
                var userId = GetUserId(user);
                await carts.UpdateItemQtyAsync(userId, itemId, req.Quantity, ct);
                var cart = await carts.GetOrCreateActiveCartAsync(userId, ct);
                return Results.Ok(cart);
            });

            group.MapDelete("/items/{itemId:guid}", async (ClaimsPrincipal user, Guid itemId, ICartRepository carts, CancellationToken ct) =>
            {
                var userId = GetUserId(user);
                await carts.RemoveItemAsync(userId, itemId, ct);
                var cart = await carts.GetOrCreateActiveCartAsync(userId, ct);
                return Results.Ok(cart);
            });

            return app;
        }

        private static Guid GetUserId(ClaimsPrincipal user)
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var guid))
                throw new UnauthorizedAccessException("Invalid token: missing user id.");

            return guid;
        }
    }
}
