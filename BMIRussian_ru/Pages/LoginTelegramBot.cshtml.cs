using BMIRussian_ru.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sibvic.AuthLib;
using Sibvic.AuthLib.Logic;
using Sibvic.AuthLib.Exceptions;

namespace BMIRussian_ru.Pages
{
    public class LoginTelegramBotModel(
        IHttpClientFactory httpClientFactory,
        ILogger<LoginTelegramBotModel> logger,
        AuthLogic authLogic,
        ApplicationDbContext context,
        IConfiguration configuration) : PageModel
    {
        public string? ErrorMessage { get; set; }
        public List<AgreementViewModel>? UnacceptedAgreements { get; set; }
        public string? TemporaryToken { get; set; }
        public string? TelegramId { get; set; }
        
        [BindProperty]
        public List<int> SelectedAgreementIds { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string? token = null, string? telegramid = null)
        {
            // Accept token and telegramid from query parameters (note: telegramid is lowercase in URL)
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(telegramid))
            {
                ErrorMessage = "Токен и Telegram ID обязательны";
                return Page();
            }

            TemporaryToken = token;
            TelegramId = telegramid;

            try
            {
                // Find user by telegram ID
                var user = authLogic.FindUser(telegramid, CredentialsSource.Telegram);
                if (user == null)
                {
                    ErrorMessage = "Пользователь не найден";
                    return Page();
                }

                // Try to authenticate - this validates the token
                // If it throws AgreementsNotAcceptedException, token is valid but agreements need acceptance
                try
                {
                    var jwtToken = authLogic.AuthenticateFromTelegramBot(telegramid, token);
                    SetJwtCookie(jwtToken);
                    return RedirectToPage("/Index");
                }
                catch (AgreementsNotAcceptedException)
                {
                    // Token is valid, but agreements need to be accepted
                    // Get agreements that need to be signed
                    var agreementsToSign = authLogic.GetAgreementsToSign(user);
                    
                    if (agreementsToSign != null && agreementsToSign.Any())
                    {
                        // Show agreements that need to be accepted
                        UnacceptedAgreements = agreementsToSign.Select(a => new AgreementViewModel
                        {
                            Id = a.Id,
                            Title = a.Title ?? "Соглашение",
                            Content = a.Description ?? ""
                        }).ToList();
                        return Page();
                    }
                    else
                    {
                        // No agreements to sign, but exception was thrown - this shouldn't happen
                        ErrorMessage = "Ошибка при проверке соглашений";
                        return Page();
                    }
                }
                catch (InvalidTokenException)
                {
                    ErrorMessage = "Неверный токен";
                    return Page();
                }
                catch (TokenExpiredException)
                {
                    ErrorMessage = "Токен истек. Пожалуйста, получите новый токен через Telegram бота.";
                    return Page();
                }
                catch (UserNotFoundException)
                {
                    ErrorMessage = "Пользователь не найден";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Telegram bot login");
                ErrorMessage = "Произошла ошибка при попытке входа. Пожалуйста, попробуйте позже.";
            }

            return Page();
        }

        private void SetJwtCookie(string jwtToken)
        {
            // If we get here, all agreements are accepted and we have the token
            // Store JWT token in cookie and redirect
            // HttpOnly is set to false to allow Blazor WebAssembly to access the cookie via JavaScript
            var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            };

            // Set domain from configuration if specified
            var cookieDomain = configuration["CookieDomain"];
            if (!string.IsNullOrWhiteSpace(cookieDomain))
            {
                cookieOptions.Domain = cookieDomain;
            }

            Response.Cookies.Append("jwtToken", jwtToken, cookieOptions);
        }

        public async Task<IActionResult> OnPostAsync(string? token = null, string? telegramid = null)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(telegramid))
            {
                ErrorMessage = "Токен и Telegram ID обязательны";
                return Page();
            }

            TemporaryToken = token;
            TelegramId = telegramid;

            try
            {
                // Find user by telegram ID
                var user = authLogic.FindUser(telegramid, CredentialsSource.Telegram);
                if (user == null)
                {
                    ErrorMessage = "Пользователь не найден";
                    return Page();
                }

                // Get agreements that need to be signed
                var agreementsToSign = authLogic.GetAgreementsToSign(user);
                
                if (agreementsToSign == null || !agreementsToSign.Any())
                {
                    // All agreements are already accepted, try to authenticate
                    try
                    {
                        var jwtToken = authLogic.AuthenticateFromTelegramBot(telegramid, token);
                        SetJwtCookie(jwtToken);
                        return RedirectToPage("/Index");
                    }
                    catch (InvalidTokenException)
                    {
                        ErrorMessage = "Неверный токен";
                        return Page();
                    }
                    catch (TokenExpiredException)
                    {
                        ErrorMessage = "Токен истек. Пожалуйста, получите новый токен через Telegram бота.";
                        return Page();
                    }
                }

                // Check if all agreements are selected
                var allAgreementIds = agreementsToSign.Select(a => a.Id).ToList();
                var allSelected = allAgreementIds.All(id => SelectedAgreementIds.Contains(id));

                if (!allSelected)
                {
                    ErrorMessage = "Необходимо принять все";
                    UnacceptedAgreements = agreementsToSign.Select(a => new AgreementViewModel
                    {
                        Id = a.Id,
                        Title = a.Title ?? "Соглашение",
                        Content = a.Description ?? ""
                    }).ToList();
                    return Page();
                }

                // Accept all selected agreements
                foreach (var agreementId in SelectedAgreementIds)
                {
                    var agreement = agreementsToSign.FirstOrDefault(a => a.Id == agreementId);
                    if (agreement != null)
                    {
                        authLogic.AcceptAgreement(user, agreement);
                    }
                }

                await context.SaveChangesAsync();

                // Check again if all agreements are now accepted
                var remainingAgreements = authLogic.GetAgreementsToSign(user);
                if (remainingAgreements != null && remainingAgreements.Any())
                {
                    ErrorMessage = "Ошибка при принятии соглашений";
                    UnacceptedAgreements = remainingAgreements.Select(a => new AgreementViewModel
                    {
                        Id = a.Id,
                        Title = a.Title ?? "Соглашение",
                        Content = a.Description ?? ""
                    }).ToList();
                    return Page();
                }

                // All agreements accepted, generate token through AuthLogic.GenerateToken
                try
                {
                    var jwtToken = authLogic.GenerateToken(user);
                    SetJwtCookie(jwtToken);
                    return RedirectToPage("/Index");
                }
                catch (AgreementsNotAcceptedException)
                {
                    // This shouldn't happen if we just accepted all agreements
                    ErrorMessage = "Ошибка: соглашения не были приняты";
                    return Page();
                }
                catch (ArgumentNullException)
                {
                    ErrorMessage = "Ошибка: пользователь не найден";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during agreement acceptance");
                ErrorMessage = "Произошла ошибка при принятии соглашений. Пожалуйста, попробуйте позже.";
            }

            return Page();
        }

    }

    public class AgreementViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}

