using SimpleeCommerceApp.Auth;
using SimpleeCommerceApp.Data.Repositories;

namespace SimpleeCommerceApp.Data.Seed
{
    public static class DemoUserSeeder
    {
        // Demo credentials (put in README)
        private const string DemoUsername = "demo";
        private const string DemoPassword = "P@ssw0rd!";

        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var existing = await users.FindByUsernameAsync(DemoUsername, ct);
            if (existing is not null) return;

            var (hash, salt) = PasswordHasher.HashPassword(DemoPassword);
            await users.CreateUserAsync(DemoUsername, hash, salt, ct);
        }
    }
}
