using BMIRussian_ru.Data;
using BMIRussian_ru.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BMIRussian_ru.Pages.Admin;

public class UploadTranscriptModel(ApplicationDbContext context, IMinioService minio, IMeilisearchService meilisearch) : PageModel
{
    public Video? Video { get; set; }

    [BindProperty]
    public IFormFile? TranscriptFile { get; set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Video = await context.Videos.FindAsync(id);
        if (Video == null)
            return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        Video = await context.Videos.FindAsync(id);
        if (Video == null)
            return NotFound();

        if (TranscriptFile == null || TranscriptFile.Length == 0)
        {
            ModelState.AddModelError(nameof(TranscriptFile), "Выберите файл для загрузки.");
            return Page();
        }

        var ext = Path.GetExtension(TranscriptFile.FileName).ToLowerInvariant();
        var allowed = new[] { ".txt", ".srt", ".vtt", ".json", ".ass", ".ssa" };
        if (!allowed.Contains(ext))
        {
            ModelState.AddModelError(nameof(TranscriptFile), "Допустимые форматы: TXT, SRT, VTT, JSON, ASS, SSA.");
            return Page();
        }

        var contentType = ext switch
        {
            ".txt" => "text/plain",
            ".srt" => "text/plain",
            ".vtt" => "text/vtt",
            ".json" => "application/json",
            ".ass" => "application/x-subrip",
            ".ssa" => "application/x-subrip",
            _ => "application/octet-stream"
        };

        await using var stream = TranscriptFile.OpenReadStream();
        await minio.UploadTranscriptAsync(id, stream, TranscriptFile.Length, contentType);

        Video.HasTranscript = true;
        await context.SaveChangesAsync();

        if (Video.Status == VideoStatus.Published)
        {
            var transcriptContent = await minio.GetTranscriptContentAsync(id);
            transcriptContent = TranscriptHelper.ExtractPlainText(transcriptContent, ext);
            await meilisearch.IndexVideoAsync(Video, transcriptContent);
        }

        TempData["UploadTranscriptMessage"] = "Транскрипт успешно загружен.";
        return RedirectToPage("/Admin/Video");
    }
}
