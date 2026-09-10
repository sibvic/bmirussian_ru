using BMIRussian_ru.Data;
using BMIRussian_ru.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sibvic.AuthLib;
using Sibvic.AuthLib.Exceptions;
using Sibvic.AuthLib.Logic;
using User = Sibvic.AuthLib.User;

namespace BMIRussian_ru.Pages
{
    public class LoginGoogleAgreementsModel(
        AuthLogic authLogic,
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<LoginGoogleAgreementsModel> logger) : PageModel
    {
        public const string PendingGoogleUserIdSessionKey = "pending_google_user_id";

        public string? ErrorMessage { get; set; }
        public List<AgreementViewModel>? UnacceptedAgreements { get; set; }

        [BindProperty]
        public List<int> SelectedAgreementIds { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var idStr = HttpContext.Session.GetString(PendingGoogleUserIdSessionKey);
            if (string.IsNullOrEmpty(idStr) || !long.TryParse(idStr, out var userId))
            {
                return RedirectToPage("/Login");
            }

            var user = await context.Set<User>().FindAsync(userId);
            if (user == null)
            {
                HttpContext.Session.Remove(PendingGoogleUserIdSessionKey);
                return RedirectToPage("/Login");
            }

            var agreementsToSign = authLogic.GetAgreementsToSign(user);
            if (agreementsToSign == null || !agreementsToSign.Any())
            {
                try
                {
                    var jwtToken = authLogic.GenerateToken(user);
                    HttpContext.Session.Remove(PendingGoogleUserIdSessionKey);
                    JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                    return RedirectToPage("/Index");
                }
                catch (AgreementsNotAcceptedException)
                {
                    ErrorMessage = "Ошибка при проверке соглашений";
                    return Page();
                }
            }

            UnacceptedAgreements = agreementsToSign.Select(a => new AgreementViewModel
            {
                Id = a.Id,
                Title = a.Title ?? "Соглашение",
                Content = a.Description ?? ""
            }).ToList();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var idStr = HttpContext.Session.GetString(PendingGoogleUserIdSessionKey);
            if (string.IsNullOrEmpty(idStr) || !long.TryParse(idStr, out var userId))
            {
                ErrorMessage = "Сессия истекла. Войдите снова."
                return Page();
            }

            var user = await context.Set<User>().FindAsync(userId);
            if (user == null)
            {
                HttpContext.Session.Remove(PendingGoogleUserIdSessionKey);
                ErrorMessage = "Пользователь не найден";
                return Page();
            }

            var agreementsToSign = authLogic.GetAgreementsToSign(user);
            if (agreementsToSign == null || !agreementsToSign.Any())
            {
                try
                {
                    var jwtToken = authLogic.GenerateToken(user);
                    HttpContext.Session.Remove(PendingGoogleUserIdSessionKey);
                    JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                    return RedirectToPage("/Index");
                }
                catch (AgreementsNotAcceptedException)
                {
                    ErrorMessage = "Ошибка при проверке соглашений";
                    return Page();
                }
            }

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

            foreach (var agreementId in SelectedAgreementIds)
            {
                var agreement = agreementsToSign.FirstOrDefault(a => a.Id == agreementId);
                if (agreement != null)
                    authLogic.AcceptAgreement(user, agreement);
            }

            await context.SaveChangesAsync();

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

            try
            {
                var jwtToken = authLogic.GenerateToken(user);
                HttpContext.Session.Remove(PendingGoogleUserIdSessionKey);
                JwtCookieHelper.AppendJwtCookie(Response, Request, configuration, jwtToken);
                return RedirectToPage("/Index");
            }
            catch (AgreementsNotAcceptedException)
            {
                ErrorMessage = "Ошибка: соглашения не были приняты";
                return Page();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Google login agreement acceptance");
                ErrorMessage = "Произошла ошибка при принятии соглашений. Пожалуйста, попробуйте позже.";
                return Page();
            }
        }
    }
}
