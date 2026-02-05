using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class ImportVideoModel(ApplicationDbContext context) : PageModel
    {
        [BindProperty]
        [Display(Name = "Данные")]
        public string Data { get; set; } = "";

        public string? Message { get; set; }
        public bool IsError { get; set; }

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Data))
            {
                ModelState.AddModelError(nameof(Data), "Введите данные для импорта.");
                return Page();
            }

            var rows = ParseTuples(Data);
            if (rows.Count == 0)
            {
                ModelState.AddModelError(nameof(Data), "Не удалось распознать ни одной строки данных. Формат: ('SeoId', 'ChannelSeoId', 'Description', 'ImageUrl', 'Date', N, 'Title', 'videoUrl', ...)");
                return Page();
            }

            var imported = 0;
            var errors = new List<string>();

            foreach (var values in rows)
            {
                if (values.Count < 8)
                {
                    errors.Add($"Строка с недостаточным количеством полей: {values.Count}");
                    continue;
                }

                var videoSeoId = values[0]?.Trim().Trim('\'') ?? "";
                var description = values[2]?.Trim().Trim('\'') ?? "";
                var imageUrl = values[3]?.Trim().Trim('\'') ?? "";
                var dateStr = values[4]?.Trim().Trim('\'') ?? "";
                var title = values[6]?.Trim().Trim('\'') ?? "";
                var videoUrl = values[7]?.Trim().Trim('\'') ?? "";

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(videoUrl))
                {
                    errors.Add($"Пропущены название или URL: '{title}' / '{videoUrl}'");
                    continue;
                }
                if (!DateTime.TryParse(dateStr, out var publishDate))
                    publishDate = DateTime.UtcNow.Date;

                var video = new Video
                {
                    Title = title,
                    SEOId = string.IsNullOrEmpty(videoSeoId) ? SeoIdHelper.FromTitle(title) : videoSeoId,
                    Description = description,
                    ImageUrl = imageUrl,
                    PublishDate = DateTime.SpecifyKind(publishDate, DateTimeKind.Utc),
                    VideoUrls = videoUrl,
                    Status = VideoStatus.Editing,
                    Keywords = ""
                };
                context.Videos.Add(video);
                imported++;
            }

            await context.SaveChangesAsync();

            if (imported > 0 && errors.Count == 0)
                return RedirectToPage("/Admin/Video");

            Message = imported > 0
                ? $"Импортировано видео: {imported}." + (errors.Count > 0 ? " Ошибки: " + string.Join("; ", errors) : "")
                : "Ошибки: " + string.Join("; ", errors);
            IsError = errors.Count > 0;
            return Page();
        }

        private static List<List<string>> ParseTuples(string text)
        {
            var result = new List<List<string>>();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '(') continue;
                var start = i + 1;
                var inString = false;
                var j = start;
                while (j < text.Length)
                {
                    var c = text[j];
                    if (inString)
                    {
                        if (c == '\'' && (j + 1 >= text.Length || text[j + 1] != '\''))
                            inString = false;
                        else if (c == '\'' && j + 1 < text.Length && text[j + 1] == '\'')
                            j++;
                    }
                    else if (c == '\'')
                        inString = true;
                    else if (c == ')')
                        break;
                    j++;
                }
                if (j < text.Length)
                {
                    var inner = text[start..j];
                    var values = ParseValues(inner);
                    if (values.Count > 0)
                        result.Add(values);
                }
                i = j;
            }
            return result;
        }

        private static List<string> ParseValues(string tupleContent)
        {
            var result = new List<string>();
            var i = 0;
            while (i < tupleContent.Length)
            {
                while (i < tupleContent.Length && (tupleContent[i] == ',' || char.IsWhiteSpace(tupleContent[i])))
                    i++;
                if (i >= tupleContent.Length) break;

                if (tupleContent[i] == '\'')
                {
                    var start = i + 1;
                    i++;
                    while (i < tupleContent.Length)
                    {
                        if (tupleContent[i] == '\'' && (i + 1 >= tupleContent.Length || tupleContent[i + 1] != '\''))
                            break;
                        if (tupleContent[i] == '\'' && i + 1 < tupleContent.Length && tupleContent[i + 1] == '\'')
                            i++;
                        i++;
                    }
                    result.Add(tupleContent[start..i].Replace("''", "'"));
                    i++;
                }
                else if (char.IsDigit(tupleContent[i]) || (tupleContent[i] == '-' && i + 1 < tupleContent.Length && char.IsDigit(tupleContent[i + 1])))
                {
                    var start = i;
                    if (tupleContent[i] == '-') i++;
                    while (i < tupleContent.Length && (char.IsDigit(tupleContent[i]) || tupleContent[i] == '.'))
                        i++;
                    result.Add(tupleContent[start..i].Trim());
                }
                else if (i + 4 <= tupleContent.Length && tupleContent.AsSpan(i, 4).Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add("");
                    i += 4;
                }
                else
                {
                    var start = i;
                    while (i < tupleContent.Length && tupleContent[i] != ',')
                        i++;
                    result.Add(tupleContent[start..i].Trim());
                }
            }
            return result;
        }
    }
}
