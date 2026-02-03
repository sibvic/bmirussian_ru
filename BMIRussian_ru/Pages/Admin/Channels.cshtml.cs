using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages.Admin
{
    public class ChannelsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ChannelsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Channel> Channels { get; set; } = new List<Channel>();

        public async Task OnGetAsync()
        {
            Channels = await _context.Channels
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.Title)
                .ToListAsync();
        }
    }
}
