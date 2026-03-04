using SimpleeCommerceApp.Api;
using SimpleeCommerceApp.Data.Repositories;
using SimpleeCommerceApp.Data;
using SimpleeCommerceApp.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SimpleeCommerceApp.Data.Seed;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

// ADO.NET infrastructure
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();

// Swagger (Dev)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Wire JWT & Auth
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddHttpClient();

var jwtSection = builder.Configuration.GetSection("Jwt");
var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}          
else
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value?.ToLowerInvariant() ?? "";

    // allow login + api auth + static files
    if (path.StartsWith("/login") || path.StartsWith("/api/auth") || path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/_framework"))
    {
        await next();
        return;
    }

    // protect main page only
    if (path == "/" || path.StartsWith("/index"))
    {
        if (!ctx.Request.Cookies.ContainsKey("access_token"))
        {
            ctx.Response.Redirect("/login");
            return;
        }
    }

    await next();
});


app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// API endpoints 
app.MapAuthEndpoints();
app.MapProductsEndpoints();
app.MapCartEndpoints();


if (app.Environment.IsDevelopment())
{
    await DemoUserSeeder.SeedAsync(app.Services);
}

app.Run();
