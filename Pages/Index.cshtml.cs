using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SimpleeCommerceApp.Models;
using static SimpleeCommerceApp.Models.CartModels;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Azure;

namespace SimpleeCommerceApp.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _http;

    public IndexModel(IHttpClientFactory http) => _http = http;

    public List<Product> Products { get; private set; } = new();
    public CartDto Cart { get; private set; } = new();
    public string? Error { get; private set; }

    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public PagedResult<Product> ProductsPage { get; private set; } = new();

    public async Task OnGetAsync(string? q, int page = 1)
    {
        Q = q;
        Page = page < 1 ? 1 : page;
        await LoadAllAsync();
    }

    public async Task<IActionResult> OnPostAddToCartAsync(Guid productId, int quantity)
    {
        try
        {
            var client = CreateAuthedClient();

            var payload = JsonSerializer.Serialize(new { productId, quantity });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/cart/items", content);
            if (!resp.IsSuccessStatusCode)
                Error = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        await LoadAllAsync();
        return RedirectToPage("/Index", new { q = Q, page = Page });
    }

    public async Task<IActionResult> OnPostUpdateQtyAsync(Guid itemId, int quantity)
    {
        try
        {
            var client = CreateAuthedClient();

            var payload = JsonSerializer.Serialize(new { quantity });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync($"/api/cart/items/{itemId}", content);
            if (!resp.IsSuccessStatusCode)
                Error = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        await LoadAllAsync();
        return RedirectToPage("/Index", new { q = Q, page = Page });
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid itemId)
    {
        try
        {
            var client = CreateAuthedClient();
            var resp = await client.DeleteAsync($"/api/cart/items/{itemId}");
            if (!resp.IsSuccessStatusCode)
                Error = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        await LoadAllAsync();
        return RedirectToPage("/Index", new { q = Q, page = Page });
    }

    private async Task LoadAllAsync()
    {
        try
        {
            var anon = CreateClient();
            var url = $"/api/products?q={Uri.EscapeDataString(Q ?? "")}&page={Page}&pageSize={PageSize}";
            ProductsPage = await GetJsonAsync<PagedResult<Product>>(anon, url) ?? new PagedResult<Product>();
            Products = ProductsPage.Items.ToList();

            var authed = CreateAuthedClient();
            Cart = await GetJsonAsync<CartDto>(authed, "/api/cart") ?? new CartDto();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _http.CreateClient();
        client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");
        return client;
    }

    private HttpClient CreateAuthedClient()
    {
        var client = CreateClient();

        if (!Request.Cookies.TryGetValue("access_token", out var token) || string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Not logged in.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<T?> GetJsonAsync<T>(HttpClient client, string url)
    {
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return default;

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public IActionResult OnPostLogout()
    {
        Response.Cookies.Delete("access_token");
        return RedirectToPage("/Login");
    }
}
