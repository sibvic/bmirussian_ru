using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BMIRussian_ru.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public LoginModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Optional URL to the Telegram bot for login (e.g. https://t.me/YourLoginBot).
        /// Set "TelegramBot:LoginBotUrl" in appsettings to show a direct link.
        /// </summary>
        public string? TelegramBotUrl { get; set; }

        public IActionResult OnGet()
        {
            TelegramBotUrl = _configuration["TelegramBot:LoginBotUrl"];
            return Page();
        }
    }
}
