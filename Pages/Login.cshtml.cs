using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text;

namespace SimpleeCommerceApp.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _http;

        public LoginModel(IConfiguration config, IHttpClientFactory http)
        {
            _config = config;
            _http = http;
        }

        [BindProperty] public string Username { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        public string? Error { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Enter username and password.";
                return Page();
            }

            var client = _http.CreateClient();
            client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");
            var payload = JsonSerializer.Serialize(new { username = Username, password = Password });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/auth/login", content);
            if (!resp.IsSuccessStatusCode)
            {
                Error = "Invalid login.";
                return Page();
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("token").GetString();

            if (string.IsNullOrWhiteSpace(token))
            {
                Error = "Login failed: token missing.";
                return Page();
            }

            // Store token securely in HttpOnly cookie
            Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddMinutes(5)
            });

            return RedirectToPage("/Index");
        }
    }
}
