using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class ImportVideoModel(
        ApplicationDbContext context,
        VideoImportService importer,
        ILogger<ImportVideoModel> logger) : PageModel
    {
        [BindProperty]
        [Display(Name = "Данные")]
        public string Data { get; set; } = "";

        public string? Message { get; set; }
        public bool IsError { get; set; }

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            logger.LogInformation("Admin video import submitted.");
            // Import by URL (YouTube / VK): one URL per line
            var (imported, errors) = await importer.ImportByUrlsAsync(Data);
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Video import finished: {Imported} added, {ErrorCount} error(s). Persisted to database.",
                imported,
                errors.Count);

            if (errors.Count > 0)
            {
                foreach (var err in errors)
                    logger.LogWarning("Import issue: {Message}", err);
            }

            if (imported > 0)
            {
                if (errors.Count == 0)
                {
                    logger.LogInformation("All URLs imported successfully; redirecting to /Admin/Video.");
                    return RedirectToPage("/Admin/Video");
                }
            }

            ModelState.AddModelError(nameof(Data), "Введите данные для импорта или URL-ы видео.");
            return Page();
        }
    }
}
