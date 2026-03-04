using Microsoft.Data.SqlClient;
using SimpleeCommerceApp.Models;

namespace SimpleeCommerceApp.Data.Repositories
{
    public interface IProductRepository
    {
        Task<PagedResult<Product>> GetActiveProductsAsync(string? search, int page, int pageSize, CancellationToken ct);
    }

    public sealed class ProductRepository : IProductRepository
    {
        private readonly ISqlConnectionFactory _db;
        public ProductRepository(ISqlConnectionFactory db) => _db = db;

        public async Task<PagedResult<Product>> GetActiveProductsAsync(string? search, int page, int pageSize, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 5 or > 50 ? 10 : pageSize;
            var q = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            const string countSql = @"
                SELECT COUNT(*)
                FROM dbo.Products
                WHERE IsActive = 1
                  AND (@q IS NULL OR Name LIKE '%' + @q + '%' OR Description LIKE '%' + @q + '%');";

            const string pageSql = @"
                SELECT Id, Name, Description, Price, ImageUrl, StockQty
                FROM dbo.Products
                WHERE IsActive = 1
                  AND (@q IS NULL OR Name LIKE '%' + @q + '%' OR Description LIKE '%' + @q + '%')
                ORDER BY CreatedAt DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);

            int totalCount;
            await using (var cmd = new SqlCommand(countSql, conn))
            {
                cmd.Parameters.AddWithValue("@q", (object?)q ?? DBNull.Value);
                totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            var offset = (page - 1) * pageSize;
            var items = new List<Product>();

            await using (var cmd = new SqlCommand(pageSql, conn))
            {
                cmd.Parameters.AddWithValue("@q", (object?)q ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    items.Add(new Product
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                        Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                        ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
                        StockQty = reader.GetInt32(reader.GetOrdinal("StockQty"))
                    });
                }
            }

            return new PagedResult<Product>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
