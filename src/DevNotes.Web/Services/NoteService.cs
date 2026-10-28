using DevNotes.Web.Data;
using DevNotes.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNotes.Web.Services;

public record NotaEntrada(string Titulo, string Conteudo, int CategoriaId, IReadOnlyCollection<int> TecnologiasIds);

public record NotaResumo(
    int Id,
    string Titulo,
    NotaEstado Estado,
    DateTime DataAtualizacao,
    string CategoriaNome,
    string Resumo,
    IReadOnlyList<string> TecnologiasNomes);

public sealed class NoteInvalidaException(IReadOnlyList<string> erros) : Exception(string.Join(" ", erros))
{
    public IReadOnlyList<string> Erros { get; } = erros;
}

public sealed class NoteService(DevNotesDbContext db)
{
    public const int LimiteTitulo = 200;
    public const int TamanhoResumo = 160;

    public static IReadOnlyList<string> Validar(NotaEntrada entrada)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(entrada.Titulo))
        {
            erros.Add("O título é obrigatório.");
        }
        else if (entrada.Titulo.Length > LimiteTitulo)
        {
            erros.Add($"O título deve ter no máximo {LimiteTitulo} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(entrada.Conteudo))
        {
            erros.Add("O conteúdo em Markdown é obrigatório.");
        }

        if (entrada.CategoriaId <= 0)
        {
            erros.Add("Selecione uma categoria.");
        }

        if (entrada.TecnologiasIds.Count == 0)
        {
            erros.Add("Informe pelo menos uma tecnologia.");
        }

        return erros;
    }

    public async Task<Nota> CriarAsync(NotaEntrada entrada)
    {
        var erros = new List<string>(Validar(entrada));

        Categoria? categoria = null;
        List<Tecnologia> tecnologias = [];

        if (erros.Count == 0)
        {
            categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == entrada.CategoriaId);
            if (categoria is null)
            {
                erros.Add("Selecione uma categoria.");
            }

            var idsSolicitados = entrada.TecnologiasIds.Distinct().ToList();
            tecnologias = await db.Tecnologias
                .Where(t => idsSolicitados.Contains(t.Id))
                .ToListAsync();

            if (tecnologias.Count != idsSolicitados.Count)
            {
                erros.Add("Informe pelo menos uma tecnologia válida.");
            }
        }

        if (erros.Count > 0)
        {
            throw new NoteInvalidaException(erros); // nada foi salvo: sem SaveChanges
        }

        var agora = DateTime.UtcNow;
        var nota = new Nota
        {
            Titulo = entrada.Titulo,
            Conteudo = entrada.Conteudo,
            Estado = NotaEstado.Rascunho, // RN-13: criação sempre em rascunho
            DataCriacao = agora,
            DataAtualizacao = agora,
            Categoria = categoria!,
            CategoriaId = categoria!.Id,
            Tecnologias = []
        };

        foreach (var tecnologia in tecnologias)
        {
            nota.Tecnologias.Add(new NotaTecnologia { Nota = nota, Tecnologia = tecnologia });
        }

        db.Add(nota);
        await db.SaveChangesAsync(); // RN-18/ADR-002: nota + classificações em uma transação única

        return nota;
    }

    public async Task<IReadOnlyList<NotaResumo>> ListarAsync()
    {
        var itens = await db.Notas
            .AsNoTracking()
            .OrderByDescending(n => n.DataAtualizacao)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotaResumo(
                n.Id,
                n.Titulo,
                n.Estado,
                n.DataAtualizacao,
                n.Categoria!.Nome,
                n.Conteudo.Length > TamanhoResumo ? n.Conteudo.Substring(0, TamanhoResumo) : n.Conteudo,
                n.Tecnologias.Select(nt => nt.Tecnologia!.Nome).ToList()))
            .ToListAsync();

        return itens;
    }

    public async Task<(IReadOnlyList<Categoria> Categorias, IReadOnlyList<Tecnologia> Tecnologias)> CarregarClassificacoesAsync()
    {
        var categorias = await db.Categorias
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .ToListAsync();
        var tecnologias = await db.Tecnologias
            .AsNoTracking()
            .OrderBy(t => t.Nome)
            .ToListAsync();

        return (categorias, tecnologias);
    }
}
