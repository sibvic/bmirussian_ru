using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BMIRussian_ru.Pages
{
    public class LogoutModel(IConfiguration configuration) : PageModel
    {
        public IActionResult OnGet()
        {
            var cookieOptions = new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            };
            var cookieDomain = configuration["CookieDomain"];
            if (!string.IsNullOrWhiteSpace(cookieDomain))
            {
                cookieOptions.Domain = cookieDomain;
            }
            Response.Cookies.Delete("jwtToken", cookieOptions);
            return RedirectToPage("/Index");
        }
    }
}
