using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using Sibvic.AuthLib;
using Sibvic.AuthLib.Exceptions;
using Sibvic.AuthLib.Google;
using Sibvic.AuthLib.Logic;
using Sibvic.AuthLib.VK;
using AuthUser = Sibvic.AuthLib.User;

namespace BMIRussian_ru.Pages
{
    public class LoginModel(
        IConfiguration configuration,
        GoogleSignInService googleSignIn,
        VkSignInService vkSignIn,
        IHttpClientFactory httpClientFactory,
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

        /// <summary>
        /// VK ID client ID. When set, a "Sign in with VK" button is shown on the login page.
        /// </summary>
        public string? VKClientId { get; set; }

        public string? VKRedirectUrl { get; set; }

        public bool VKSignInConfigured => !string.IsNullOrWhiteSpace(VKClientId);

        /// <summary>After successful sign-in or credential link, redirect here if the URL is local (e.g. /Profile).</summary>
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public IActionResult OnGet()
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];
            VKClientId = configuration["VK:ClientId"];
            VKRedirectUrl = configuration["VK:RedirectUri"] ?? $"{GetBaseUrl().TrimEnd('/')}/Login";
            return Page();
        }

        public IActionResult OnGetVkLogin()
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];
            VKClientId = configuration["VK:ClientId"];

            if (!vkSignIn.IsConfigured)
            {
                TempData["LoginError"] = "Вход через VK не настроен на сервере.";
                return RedirectToLoginWithReturn();
            }

            var redirectUri = GetVkRedirectUri();
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                TempData["LoginError"] = "Не задан redirect URI для входа через VK.";
                return RedirectToLoginWithReturn();
            }

            var (verifier, challenge) = GeneratePkce();
            var state = GenerateState();

            HttpContext.Session.SetString("vk:verifier", verifier);
            HttpContext.Session.SetString("vk:state", state);
            HttpContext.Session.SetString("vk:returnUrl", ReturnUrl ?? "");

            var url = $"https://id.vk.ru/authorize?response_type=code" +
                      $"&client_id={Uri.EscapeDataString(VKClientId!)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&state={Uri.EscapeDataString(state)}" +
                      $"&code_challenge={Uri.EscapeDataString(challenge)}" +
                      $"&code_challenge_method=S256" +
                      $"&scope={Uri.EscapeDataString("email")}";

            return Redirect(url);
        }

        public async Task<IActionResult> OnGetVkCallbackAsync(
            string? code,
            string? state,
            string? device_id,
            string? error,
            string? error_description,
            CancellationToken cancellationToken)
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];
            VKClientId = configuration["VK:ClientId"];

            if (!string.IsNullOrWhiteSpace(error))
            {
                logger.LogWarning("VK OAuth error: {Error} {Description}", error, error_description);
                TempData["LoginError"] = $"Ошибка входа через VK: {error}";
                return RedirectToLoginWithReturn();
            }

            var savedState = HttpContext.Session.GetString("vk:state");
            var verifier = HttpContext.Session.GetString("vk:verifier");
            var returnUrl = HttpContext.Session.GetString("vk:returnUrl");

            HttpContext.Session.Remove("vk:state");
            HttpContext.Session.Remove("vk:verifier");
            HttpContext.Session.Remove("vk:returnUrl");

            if (string.IsNullOrWhiteSpace(savedState)
                || savedState != state
                || string.IsNullOrWhiteSpace(verifier)
                || string.IsNullOrWhiteSpace(code))
            {
                TempData["LoginError"] = "Некорректный или устаревший запрос входа через VK.";
                return RedirectToLoginWithReturn();
            }

            var redirectUri = GetVkRedirectUri();
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                TempData["LoginError"] = "Не задан redirect URI для входа через VK.";
                return RedirectToLoginWithReturn();
            }

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = VKClientId!,
                ["code_verifier"] = verifier,
                ["device_id"] = device_id ?? "",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["state"] = savedState
            };

            using var client = httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync(
                "https://id.vk.ru/oauth2/auth",
                new FormUrlEncodedContent(form),
                cancellationToken);

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("VK token exchange failed: {StatusCode} {Body}", tokenResponse.StatusCode, tokenJson);
                TempData["LoginError"] = "Не удалось обменять код авторизации VK.";
                return RedirectToLoginWithReturn();
            }

            var tokenObj = JObject.Parse(tokenJson);
            var accessToken = tokenObj["access_token"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                TempData["LoginError"] = "VK не вернул токен доступа.";
                return RedirectToLoginWithReturn();
            }

            if (tokenObj["state"]?.Value<string>() is string responseState && responseState != savedState)
            {
                TempData["LoginError"] = "Некорректный ответ от VK.";
                return RedirectToLoginWithReturn();
            }

            if (await vkSignIn.ValidateAccessTokenAsync(accessToken, cancellationToken) is not { } payload)
            {
                TempData["LoginError"] = "Не удалось подтвердить вход через VK.";
                return RedirectToLoginWithReturn();
            }

            var sourceId = VkSignInService.CredentialSourceId(payload.UserId);

            var linkUser = await TryResolveLinkUserAsync(cancellationToken);
            AuthUser? user;

            if (linkUser != null)
            {
                var linked = await authLogic.TryAddCredentialForLinkUserAsync(
                    linkUser,
                    sourceId,
                    CredentialsSource.VKAccount,
                    cancellationToken);
                if (!linked)
                {
                    TempData["LoginError"] =
                        "Этот аккаунт VK уже привязан к другому пользователю.";
                    return RedirectToLoginWithReturn();
                }

                user = linkUser;
            }
            else
            {
                user = authLogic.FindUser(sourceId, CredentialsSource.VKAccount);
                if (user == null)
                {
                    user = await authLogic.RegisterUser(
                        sourceId,
                        payload.FirstName,
                        payload.LastName,
                        username: payload.Email ?? sourceId,
                        photo_url: payload.Avatar,
                        auth_date: null,
                        hash: null,
                        CredentialsSource.VKAccount,
                        cancellationToken);
                }
            }

            if (user == null)
            {
                TempData["LoginError"] = "Не удалось зарегистрировать пользователя.";
                return RedirectToLoginWithReturn();
            }

            try
            {
                var jwtToken = authLogic.GenerateToken(user);
                JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                if (IsSafeLocalRedirect(returnUrl))
                    return Redirect(returnUrl!);
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
                return RedirectToLoginWithReturn();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "VK sign-in failed after token validation");
                TempData["LoginError"] = "Произошла ошибка при входе. Попробуйте позже.";
                return RedirectToLoginWithReturn();
            }
        }

        public async Task<IActionResult> OnPostVkSignInAsync(string? accessToken, string? returnUrl, CancellationToken cancellationToken)
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];
            VKClientId = configuration["VK:ClientId"];

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                TempData["LoginError"] = "Не удалось получить токен VK.";
                return RedirectToLoginWithReturn();
            }

            if (!vkSignIn.IsConfigured)
            {
                TempData["LoginError"] = "Вход через VK не настроен на сервере.";
                return RedirectToLoginWithReturn();
            }

            if (await vkSignIn.ValidateAccessTokenAsync(accessToken, cancellationToken) is not { } payload)
            {
                TempData["LoginError"] = "Не удалось подтвердить вход через VK.";
                return RedirectToLoginWithReturn();
            }

            var sourceId = VkSignInService.CredentialSourceId(payload.UserId);

            var linkUser = await TryResolveLinkUserAsync(cancellationToken);
            AuthUser? user;

            if (linkUser != null)
            {
                var linked = await authLogic.TryAddCredentialForLinkUserAsync(
                    linkUser,
                    sourceId,
                    CredentialsSource.VKAccount,
                    cancellationToken);
                if (!linked)
                {
                    TempData["LoginError"] =
                        "Этот аккаунт VK уже привязан к другому пользователю.";
                    return RedirectToLoginWithReturn();
                }

                user = linkUser;
            }
            else
            {
                user = authLogic.FindUser(sourceId, CredentialsSource.VKAccount);
                if (user == null)
                {
                    user = await authLogic.RegisterUser(
                        sourceId,
                        payload.FirstName,
                        payload.LastName,
                        username: payload.Email ?? sourceId,
                        photo_url: payload.Avatar,
                        auth_date: null,
                        hash: null,
                        CredentialsSource.VKAccount,
                        cancellationToken);
                }
            }

            if (user == null)
            {
                TempData["LoginError"] = "Не удалось зарегистрировать пользователя.";
                return RedirectToLoginWithReturn();
            }

            try
            {
                var jwtToken = authLogic.GenerateToken(user);
                JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                if (IsSafeLocalRedirect(returnUrl))
                    return Redirect(returnUrl!);
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
                return RedirectToLoginWithReturn();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "VK sign-in failed after token validation");
                TempData["LoginError"] = "Произошла ошибка при входе. Попробуйте позже.";
                return RedirectToLoginWithReturn();
            }
        }

        public async Task<IActionResult> OnPostGoogleSignInAsync(string? credential, CancellationToken cancellationToken)
        {
            TelegramBotUrl = configuration["TelegramBot:LoginBotUrl"];
            GoogleClientId = configuration["Google:ClientId"];
            VKClientId = configuration["VK:ClientId"];

            if (string.IsNullOrWhiteSpace(credential))
            {
                TempData["LoginError"] = "Не удалось получить учётные данные Google.";
                return RedirectToLoginWithReturn();
            }

            if (!googleSignIn.IsConfigured)
            {
                TempData["LoginError"] = "Вход через Google не настроен на сервере.";
                return RedirectToLoginWithReturn();
            }

            if (await googleSignIn.ValidateIdTokenAsync(credential, cancellationToken) is not { } payload)
            {
                TempData["LoginError"] = "Не удалось подтвердить вход через Google.";
                return RedirectToLoginWithReturn();
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
                    return RedirectToLoginWithReturn();
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
                return RedirectToLoginWithReturn();
            }

            try
            {
                var jwtToken = authLogic.GenerateToken(user);
                JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                if (IsSafeLocalRedirect(ReturnUrl))
                    return Redirect(ReturnUrl!);
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
                return RedirectToLoginWithReturn();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google sign-in failed after token validation");
                TempData["LoginError"] = "Произошла ошибка при входе. Попробуйте позже.";
                return RedirectToLoginWithReturn();
            }
        }

        private IActionResult RedirectToLoginWithReturn()
        {
            if (IsSafeLocalRedirect(ReturnUrl))
                return RedirectToPage("/Login", new { returnUrl = ReturnUrl });
            return RedirectToPage("/Login");
        }

        private bool IsSafeLocalRedirect(string? url) =>
            !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url);

        private string? GetBaseUrl()
        {
            var baseUrl = configuration["SelfUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
            }
            return baseUrl;
        }

        private string? GetVkRedirectUri()
        {
            var configured = configuration["VK:RedirectUri"];
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var baseUrl = GetBaseUrl();
            var callbackPath = Url.Page("/Login", new { handler = "VkCallback" });
            if (string.IsNullOrWhiteSpace(callbackPath))
                return null;

            return baseUrl.TrimEnd('/') + callbackPath;
        }

        private static (string verifier, string challenge) GeneratePkce()
        {
            var verifierBytes = new byte[64];
            RandomNumberGenerator.Fill(verifierBytes);
            var verifier = Base64UrlEncode(verifierBytes);

            using var sha = SHA256.Create();
            var challenge = Base64UrlEncode(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));

            return (verifier, challenge);
        }

        private string GenerateState()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Current user when JWT cookie is valid (linking VK to an existing session).
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
