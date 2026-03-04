using Microsoft.Data.SqlClient;
using static SimpleeCommerceApp.Models.CartModels;

namespace SimpleeCommerceApp.Data.Repositories
{
    public interface ICartRepository
    {
        Task<CartDto> GetOrCreateActiveCartAsync(Guid userId, CancellationToken ct);
        Task AddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct);
        Task UpdateItemQtyAsync(Guid userId, Guid itemId, int quantity, CancellationToken ct);
        Task RemoveItemAsync(Guid userId, Guid itemId, CancellationToken ct);
    }

    public sealed class CartRepository : ICartRepository
    {
        private readonly ISqlConnectionFactory _db;
        public CartRepository(ISqlConnectionFactory db) => _db = db;

        public async Task<CartDto> GetOrCreateActiveCartAsync(Guid userId, CancellationToken ct)
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);

            // 1) Get active cart id (or create)
            var cartId = await GetActiveCartIdAsync(conn, userId, ct);
            if (cartId == Guid.Empty)
            {
                cartId = Guid.NewGuid();
                const string insertCart = @"
                    INSERT INTO dbo.Carts (Id, UserId, Status)
                    VALUES (@Id, @UserId, 0);";
                await using var ins = new SqlCommand(insertCart, conn);
                ins.Parameters.AddWithValue("@Id", cartId);
                ins.Parameters.AddWithValue("@UserId", userId);
                await ins.ExecuteNonQueryAsync(ct);
            }

            return await LoadCartAsync(conn, cartId, ct);
        }

        public async Task AddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be > 0.");

            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);
            await using SqlTransaction tx = conn.BeginTransaction();

            try
            {
                var cartId = await GetActiveCartIdAsync(conn, userId, ct, tx);
                if (cartId == Guid.Empty)
                {
                    cartId = Guid.NewGuid();
                    const string insertCart = @"
                        INSERT INTO dbo.Carts (Id, UserId, Status)
                        VALUES (@Id, @UserId, 0);";

                    await using var ins = new SqlCommand(insertCart, conn, (SqlTransaction)tx);
                    ins.Parameters.AddWithValue("@Id", cartId);
                    ins.Parameters.AddWithValue("@UserId", userId);
                    await ins.ExecuteNonQueryAsync(ct);
                }

                const string getProduct = @"
                    SELECT TOP 1 Name, Price, StockQty, IsActive
                    FROM dbo.Products
                    WHERE Id = @ProductId;";

                string? productName = null;
                decimal price = 0;
                int stock = 0;
                bool isActive = false;

                await using (var cmd = new SqlCommand(getProduct, conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (!await r.ReadAsync(ct))
                        throw new InvalidOperationException("Product not found.");

                    productName = r.GetString(r.GetOrdinal("Name"));
                    price = r.GetDecimal(r.GetOrdinal("Price"));
                    stock = r.GetInt32(r.GetOrdinal("StockQty"));
                    isActive = r.GetBoolean(r.GetOrdinal("IsActive"));
                }

                if (!isActive) throw new InvalidOperationException("Product is not available.");

                // Upsert item (increment if exists)
                // Also check stock against (existing qty + added qty)
                const string getExistingQty = @"
                    SELECT TOP 1 Quantity
                    FROM dbo.CartItems
                    WHERE CartId = @CartId AND ProductId = @ProductId;";

                int existingQty = 0;
                await using (var cmd = new SqlCommand(getExistingQty, conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@CartId", cartId);
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    var obj = await cmd.ExecuteScalarAsync(ct);
                    if (obj is not null && obj != DBNull.Value)
                        existingQty = Convert.ToInt32(obj);
                }

                if (existingQty + quantity > stock)
                    throw new InvalidOperationException("Not enough stock for requested quantity.");

                if (existingQty > 0)
                {
                    const string update = @"
                        UPDATE dbo.CartItems
                        SET Quantity = Quantity + @Qty,
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE CartId = @CartId AND ProductId = @ProductId;";

                    await using var cmd = new SqlCommand(update, conn, tx);
                    cmd.Parameters.AddWithValue("@Qty", quantity);
                    cmd.Parameters.AddWithValue("@CartId", cartId);
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                else
                {
                    const string insert = @"
                        INSERT INTO dbo.CartItems (Id, CartId, ProductId, Quantity, UnitPriceAtAdd)
                        VALUES (@Id, @CartId, @ProductId, @Qty, @UnitPrice);";
                    await using var cmd = new SqlCommand(insert, conn, tx);
                    cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                    cmd.Parameters.AddWithValue("@CartId", cartId);
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    cmd.Parameters.AddWithValue("@Qty", quantity);
                    cmd.Parameters.AddWithValue("@UnitPrice", price);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                const string touchCart = @"
                    UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;";

                await using (var cmd = new SqlCommand(touchCart, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@CartId", cartId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task UpdateItemQtyAsync(Guid userId, Guid itemId, int quantity, CancellationToken ct)
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);
            await using SqlTransaction tx = conn.BeginTransaction();

            try
            {
                // Ensure item belongs to user's active cart
                var (cartId, productId, currentQty) = await GetItemOwnershipAsync(conn, userId, itemId, ct, tx);

                if (quantity <= 0)
                {
                    // treat as remove
                    const string del = @"DELETE FROM dbo.CartItems WHERE Id = @ItemId;";
                    await using var cmd = new SqlCommand(del, conn, (SqlTransaction)tx);
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                else
                {
                    // Validate stock
                    const string getStock = @"SELECT StockQty, IsActive FROM dbo.Products WHERE Id = @ProductId;";
                    int stock = 0; bool active = false;
                    await using (var cmd = new SqlCommand(getStock, conn, (SqlTransaction)tx))
                    {
                        cmd.Parameters.AddWithValue("@ProductId", productId);
                        await using var r = await cmd.ExecuteReaderAsync(ct);
                        if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Product not found.");
                        stock = r.GetInt32(0);
                        active = r.GetBoolean(1);
                    }
                    if (!active) throw new InvalidOperationException("Product is not available.");
                    if (quantity > stock) throw new InvalidOperationException("Not enough stock.");

                    const string upd = @"
                        UPDATE dbo.CartItems
                        SET Quantity = @Qty,
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE Id = @ItemId;";
                    await using var cmd2 = new SqlCommand(upd, conn, (SqlTransaction)tx);
                    cmd2.Parameters.AddWithValue("@Qty", quantity);
                    cmd2.Parameters.AddWithValue("@ItemId", itemId);
                    await cmd2.ExecuteNonQueryAsync(ct);
                }

                const string touchCart = @"UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;";
                await using (var cmd = new SqlCommand(touchCart, conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@CartId", cartId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task RemoveItemAsync(Guid userId, Guid itemId, CancellationToken ct)
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);
            await using SqlTransaction tx = conn.BeginTransaction();

            try
            {
                var (cartId, _, _) = await GetItemOwnershipAsync(conn, userId, itemId, ct, tx);
                const string del = @"DELETE FROM dbo.CartItems WHERE Id = @ItemId;";
                await using (var cmd = new SqlCommand(del, conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                const string touchCart = @"UPDATE dbo.Carts SET UpdatedAt = SYSUTCDATETIME() WHERE Id = @CartId;";
                await using (var cmd = new SqlCommand(touchCart, conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@CartId", cartId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ===== helpers =====

        private static async Task<Guid> GetActiveCartIdAsync(SqlConnection conn, Guid userId, CancellationToken ct, SqlTransaction? tx = null)
        {
            const string sql = @"SELECT TOP 1 Id FROM dbo.Carts WHERE UserId = @UserId AND Status = 0;";
            await using var cmd = tx is null ? new SqlCommand(sql, conn) : new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var obj = await cmd.ExecuteScalarAsync(ct);
            return (obj is null || obj == DBNull.Value) ? Guid.Empty : (Guid)obj;
        }

        private static async Task<CartDto> LoadCartAsync(SqlConnection conn, Guid cartId, CancellationToken ct)
        {
            const string sql = @"
                SELECT
                    ci.Id AS ItemId,
                    p.Id AS ProductId,
                    p.Name AS ProductName,
                    ci.UnitPriceAtAdd AS UnitPrice,
                    ci.Quantity
                FROM dbo.CartItems ci
                JOIN dbo.Products p ON p.Id = ci.ProductId
                WHERE ci.CartId = @CartId
                ORDER BY ci.CreatedAt DESC;";

            var items = new List<CartItemDto>();
            await using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@CartId", cartId);

                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    items.Add(new CartItemDto
                    {
                        ItemId = r.GetGuid(r.GetOrdinal("ItemId")),
                        ProductId = r.GetGuid(r.GetOrdinal("ProductId")),
                        ProductName = r.GetString(r.GetOrdinal("ProductName")),
                        UnitPrice = r.GetDecimal(r.GetOrdinal("UnitPrice")),
                        Quantity = r.GetInt32(r.GetOrdinal("Quantity"))
                    });
                }
            }

            var totalItems = items.Sum(i => i.Quantity);
            var subTotal = items.Sum(i => i.UnitPrice * i.Quantity);

            return new CartDto
            {
                CartId = cartId,
                Items = items,
                TotalItems = totalItems,
                SubTotal = subTotal
            };
        }

        private static async Task<(Guid cartId, Guid productId, int currentQty)> GetItemOwnershipAsync(
            SqlConnection conn, Guid userId, Guid itemId, CancellationToken ct, SqlTransaction tx)
        {
            const string sql = @"
                SELECT TOP 1 c.Id AS CartId, ci.ProductId, ci.Quantity
                FROM dbo.CartItems ci
                JOIN dbo.Carts c ON c.Id = ci.CartId
                WHERE ci.Id = @ItemId AND c.UserId = @UserId AND c.Status = 0;";

            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new UnauthorizedAccessException("Cart item not found for current user.");

            var cartId = r.GetGuid(r.GetOrdinal("CartId"));
            var productId = r.GetGuid(r.GetOrdinal("ProductId"));
            var qty = r.GetInt32(r.GetOrdinal("Quantity"));

            return (cartId, productId, qty);
        }
    }
}
