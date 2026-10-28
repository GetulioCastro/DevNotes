using System.ComponentModel.DataAnnotations;
using DevNotes.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DevNotes.Web.Pages.Admin.Notas;

public class CriarModel(NoteService servico) : PageModel
{
    public class EntradaNotaModel
    {
        [Required(ErrorMessage = "O título é obrigatório.")]
        [StringLength(NoteService.LimiteTitulo, ErrorMessage = "O título deve ter no máximo {1} caracteres.")]
        public string Titulo { get; set; } = "";

        [Required(ErrorMessage = "O conteúdo em Markdown é obrigatório.")]
        public string Conteudo { get; set; } = "";

        [Required(ErrorMessage = "Selecione uma categoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria.")]
        public int? CategoriaId { get; set; }

        public List<int> TecnologiasIds { get; set; } = [];
    }

    [BindProperty]
    public EntradaNotaModel Entrada { get; set; } = new();

    public SelectList Categorias { get; private set; } = new(Enumerable.Empty<object>());
    public IReadOnlyList<Models.Tecnologia> Tecnologias { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await CarregarClassificacoesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Entrada.TecnologiasIds.Count == 0)
        {
            ModelState.AddModelError($"{nameof(Entrada)}.{nameof(Entrada.TecnologiasIds)}", "Informe pelo menos uma tecnologia.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarClassificacoesAsync(); // re-render preserva os dados digitados (UI-04.validacao)
            return Page();
        }

        try
        {
            await servico.CriarAsync(new NotaEntrada(
                Entrada.Titulo,
                Entrada.Conteudo,
                Entrada.CategoriaId!.Value,
                Entrada.TecnologiasIds));
        }
        catch (NoteInvalidaException ex)
        {
            foreach (var erro in ex.Erros)
            {
                ModelState.AddModelError(string.Empty, erro);
            }

            await CarregarClassificacoesAsync();
            return Page();
        }

        TempData["Flash"] = "Nota salva"; // UI-04.sucesso → UI-03 com flash
        return RedirectToPage("./Index");
    }

    private async Task CarregarClassificacoesAsync()
    {
        var (categorias, tecnologias) = await servico.CarregarClassificacoesAsync();
        Categorias = new SelectList(categorias, nameof(Models.Categoria.Id), nameof(Models.Categoria.Nome));
        Tecnologias = tecnologias;
    }
}
