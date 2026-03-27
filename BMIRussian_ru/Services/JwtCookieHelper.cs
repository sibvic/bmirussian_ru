using Microsoft.AspNetCore.Http;

namespace BMIRussian_ru.Services
{
    public static class JwtCookieHelper
    {
        public static void AppendJwtCookie(HttpResponse response, HttpRequest request, IConfiguration configuration, string jwtToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                Secure = request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            };

            var cookieDomain = configuration["CookieDomain"];
            if (!string.IsNullOrWhiteSpace(cookieDomain))
                cookieOptions.Domain = cookieDomain;

            response.Cookies.Append("jwtToken", jwtToken, cookieOptions);
        }
    }
}
