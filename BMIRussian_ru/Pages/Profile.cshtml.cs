using System.Security.Claims;
using BMIRussian_ru.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sibvic.AuthLib;
using AuthUser = Sibvic.AuthLib.User;

namespace BMIRussian_ru.Pages
{
    [Authorize]
    public class ProfileModel(ApplicationDbContext dbContext, IConfiguration configuration) : PageModel
    {
        public string? DisplayName { get; private set; }

        public string Nickname { get; private set; } = "";

        public IReadOnlyList<ProfileCredentialRow> Credentials { get; private set; } = [];

        public bool CanLinkGoogle { get; private set; }

        public bool CanLinkTelegram { get; private set; }

        public bool CanLinkVK { get; private set; }

        public bool GoogleSignInConfigured => !string.IsNullOrWhiteSpace(configuration["Google:ClientId"]);

        public bool VKSignInConfigured => !string.IsNullOrWhiteSpace(configuration["VK:ClientId"]);

        public string? TelegramBotUrl => configuration["TelegramBot:LoginBotUrl"];

        public string? ProfileSuccessMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            ProfileSuccessMessage = TempData["ProfileMessage"] as string;

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                return Challenge();

            var user = await dbContext.Set<AuthUser>().AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return NotFound();

            Nickname = user.Nickname;
            var nameParts = new[] { user.FirstName, user.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            DisplayName = nameParts.Length > 0 ? string.Join(' ', nameParts) : null;

            var creds = await dbContext.Set<UserCredentianl>().AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Source)
                .ThenBy(c => c.SourceId)
                .ToListAsync(cancellationToken);

            Credentials = creds.Select(c => new ProfileCredentialRow(
                FormatSource((CredentialsSource)c.Source),
                c.SourceId)).ToList();

            var sources = creds.Select(c => (CredentialsSource)c.Source).ToHashSet();
            CanLinkGoogle = GoogleSignInConfigured && !sources.Contains(CredentialsSource.GoogleAccount);
            CanLinkVK = VKSignInConfigured && !sources.Contains(CredentialsSource.VKAccount);
            CanLinkTelegram = !sources.Contains(CredentialsSource.Telegram);

            return Page();
        }

        private static string FormatSource(CredentialsSource source) => source switch
        {
            CredentialsSource.Telegram => "Telegram",
            CredentialsSource.GoogleAccount => "Google",
            CredentialsSource.VKAccount => "VK",
            _ => source.ToString()
        };
    }

    public record ProfileCredentialRow(string SourceLabel, string SourceId);
}
