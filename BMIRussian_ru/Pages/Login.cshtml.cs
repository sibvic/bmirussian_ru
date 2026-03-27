using System.Globalization;
using System.Security.Claims;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sibvic.AuthLib;
using Sibvic.AuthLib.Exceptions;
using Sibvic.AuthLib.Google;
using Sibvic.AuthLib.Logic;
using AuthUser = Sibvic.AuthLib.User;

namespace BMIRussian_ru.Pages
{
    public class LoginModel(
        IConfiguration configuration,
        GoogleSignInService googleSignIn,
        AuthLogic authLogic,
        ApplicationDbContext dbContext,
        ILogger<LoginModel> logger) : PageModel
    {
        /// <summary>
        /// Optional URL to the Telegram bot for login (e.g. https://t.me/YourLoginBot).
        /// Set "TelegramBot:LoginBotUrl" in appsettings to show a direct link.
        /// </summary>
        public string? TelegramBotUrl { get; set; }

        /// <summary>
        /// OAuth 2.0 Web client ID from Google Cloud Console. When set, Google Sign-In is shown on the login page.
        /// </summary>
        public string? GoogleClientId { get; set; }

        public bool GoogleSignInConfigured => !string.IsNullOrWhiteSpace(GoogleClientId);

        public IActionResult OnGet()
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];
            return Page();
        }

        public async Task<IActionResult> OnPostGoogleSignInAsync(string? credential, CancellationToken cancellationToken)
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];

            if (string.IsNullOrWhiteSpace(credential))
            {
                TempData["LoginError"] = "Не удалось получить учётные данные Google.";
                return RedirectToPage();
            }

            if (!googleSignIn.IsConfigured)
            {
                TempData["LoginError"] = "Вход через Google не настроен на сервере.";
                return RedirectToPage();
            }

            if (await googleSignIn.ValidateIdTokenAsync(credential, cancellationToken) is not { } payload)
            {
                TempData["LoginError"] = "Не удалось подтвердить вход через Google.";
                return RedirectToPage();
            }

            var sourceId = GoogleSignInService.CredentialSourceId(payload.Subject);

            var linkUser = await TryResolveLinkUserAsync(cancellationToken);
            AuthUser? user;

            if (linkUser != null)
            {
                var linked = await authLogic.TryAddCredentialForLinkUserAsync(
                    linkUser,
                    sourceId,
                    CredentialsSource.GoogleAccount,
                    cancellationToken);
                if (!linked)
                {
                    TempData["LoginError"] =
                        "Этот аккаунт Google уже привязан к другому пользователю.";
                    return RedirectToPage();
                }

                user = linkUser;
            }
            else
            {
                user = authLogic.FindUser(sourceId, CredentialsSource.GoogleAccount);
                if (user == null)
                {
                    user = await authLogic.RegisterUser(
                        sourceId,
                        payload.GivenName,
                        payload.FamilyName,
                        username: payload.Email ?? sourceId,
                        photo_url: payload.Picture,
                        auth_date: null,
                        hash: null,
                        CredentialsSource.GoogleAccount,
                        cancellationToken);
                }
            }

            if (user == null)
            {
                TempData["LoginError"] = "Не удалось зарегистрировать пользователя.";
                return RedirectToPage();
            }

            try
            {
                var jwtToken = authLogic.GenerateToken(user);
                JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                return RedirectToPage("/Index");
            }
            catch (AgreementsNotAcceptedException)
            {
                var agreementsToSign = authLogic.GetAgreementsToSign(user);
                if (agreementsToSign != null && agreementsToSign.Any())
                {
                    HttpContext.Session.SetString(LoginGoogleAgreementsModel.PendingGoogleUserIdSessionKey, user.Id.ToString(CultureInfo.InvariantCulture));
                    return RedirectToPage("/LoginGoogleAgreements");
                }

                TempData["LoginError"] = "Ошибка при проверке соглашений";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google sign-in failed after token validation");
                TempData["LoginError"] = "Произошла ошибка при входе. Попробуйте позже.";
                return RedirectToPage();
            }
        }

        /// <summary>
        /// Current user when JWT cookie is valid (linking Google to an existing session).
        /// </summary>
        private async Task<AuthUser?> TryResolveLinkUserAsync(CancellationToken cancellationToken)
        {
            if (User.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                return null;

            return await dbContext.Set<AuthUser>().FindAsync(new object[] { userId }, cancellationToken);
        }
    }
}
