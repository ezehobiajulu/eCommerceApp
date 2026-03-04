using Microsoft.Data.SqlClient;

namespace SimpleeCommerceApp.Data.Repositories
{
    public sealed record UserRow(Guid Id, string Username, byte[] PasswordHash, byte[] PasswordSalt);

    public interface IUserRepository
    {
        Task<UserRow?> FindByUsernameAsync(string username, CancellationToken ct);
        Task<Guid> CreateUserAsync(string username, byte[] hash, byte[] salt, CancellationToken ct);
    }

    public sealed class UserRepository : IUserRepository
    {
        private readonly ISqlConnectionFactory _db;
        public UserRepository(ISqlConnectionFactory db) => _db = db;

        public async Task<UserRow?> FindByUsernameAsync(string username, CancellationToken ct)
        {
            const string sql = @"
                SELECT TOP 1 Id, Username, PasswordHash, PasswordSalt
                FROM dbo.Users
                WHERE Username = @Username;";

            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Username", username);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return new UserRow(
                r.GetGuid(0),
                r.GetString(1),
                (byte[])r["PasswordHash"],
                (byte[])r["PasswordSalt"]
            );
        }

        public async Task<Guid> CreateUserAsync(string username, byte[] hash, byte[] salt, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO dbo.Users (Id, Username, PasswordHash, PasswordSalt)
                VALUES (@Id, @Username, @Hash, @Salt);";

            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Username", username);
            cmd.Parameters.AddWithValue("@Hash", hash);
            cmd.Parameters.AddWithValue("@Salt", salt);

            await cmd.ExecuteNonQueryAsync(ct);
            return id;
        }
    }
}
