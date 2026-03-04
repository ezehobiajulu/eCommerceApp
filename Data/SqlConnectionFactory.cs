using Microsoft.Data.SqlClient;

namespace SimpleeCommerceApp.Data
{
    public interface ISqlConnectionFactory
    {
        SqlConnection CreateConnection();
    }

    public sealed class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("Sql")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Sql in appsettings.json");
        }

        public SqlConnection CreateConnection()
            => new SqlConnection(_connectionString);
    }
}
