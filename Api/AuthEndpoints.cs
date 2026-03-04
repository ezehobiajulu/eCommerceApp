using SimpleeCommerceApp.Auth;
using SimpleeCommerceApp.Data.Repositories;

namespace SimpleeCommerceApp.Api
{
    public static class AuthEndpoints
    {
        public sealed record LoginRequest(string Username, string Password);

        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth");
            group.MapPost("/login", async (
                LoginRequest req,
                IUserRepository users,
                IJwtTokenService jwt,
                CancellationToken ct) =>
            {
                var user = await users.FindByUsernameAsync(req.Username, ct);
                if (user is null)
                    return Results.Unauthorized();

                var ok = PasswordHasher.Verify(req.Password, user.PasswordSalt, user.PasswordHash);
                if (!ok) return Results.Unauthorized();

                var token = jwt.CreateToken(user.Id, user.Username);
                return Results.Ok(new { token });
            });

            return app;
        }
    }
}
