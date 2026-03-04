using SimpleeCommerceApp.Data.Repositories;

namespace SimpleeCommerceApp.Api
{
    public static class ProductsEndpoints
    {
        public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/products");
            group.MapGet("/", async (
                string? q,
                int page,
                int pageSize,
                IProductRepository repo,
                CancellationToken ct) =>
            {
                var result = await repo.GetActiveProductsAsync(q, page, pageSize, ct);
                return Results.Ok(result);
            });

            return app;
        }
    }
}
