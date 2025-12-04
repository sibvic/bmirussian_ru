using BMIRussian_ru.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using Newtonsoft.Json;

namespace BMIRussian_ru.Pages
{
    public class LoginTelegramBotModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LoginTelegramBotModel> _logger;

        public LoginTelegramBotModel(IHttpClientFactory httpClientFactory, ILogger<LoginTelegramBotModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string? token = null, string? telegramid = null)
        {
            // Accept token and telegramid from query parameters (note: telegramid is lowercase in URL)
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(telegramid))
            {
                ErrorMessage = "Токен и Telegram ID обязательны";
                return Page();
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var request = new FromTelegramBotRequest
                {
                    TemporaryToken = token,
                    TelegramId = telegramid
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{Request.Scheme}://{Request.Host}/auth/fromtelegrambot", content);

                if (response.IsSuccessStatusCode)
                {
                    var jwtToken = await response.Content.ReadAsStringAsync();
                    jwtToken = jwtToken.Trim('"'); // Remove quotes if present
                    
                    // Store JWT token in cookie
                    Response.Cookies.Append("jwtToken", jwtToken, new Microsoft.AspNetCore.Http.CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });

                    // Redirect to root page
                    return RedirectToPage("/Index");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "Неверный токен или токен истек. Пожалуйста, получите новый токен через Telegram бота."
                        : $"Ошибка при входе: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Telegram bot login");
                ErrorMessage = "Произошла ошибка при попытке входа. Пожалуйста, попробуйте позже.";
            }

            return Page();
        }
    }
}

