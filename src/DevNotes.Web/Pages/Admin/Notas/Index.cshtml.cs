using DevNotes.Web.Models;
using DevNotes.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevNotes.Web.Pages.Admin.Notas;

public class IndexModel(NoteService servico) : PageModel
{
    public IReadOnlyList<NotaResumo> Notas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Notas = await servico.ListarAsync();
    }
}
