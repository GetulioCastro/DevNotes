using DevNotes.Web.Data;
using DevNotes.Web.Models;
using DevNotes.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevNotes.Tests;

public class NoteServiceTests
{
    private static SqliteConnection AbrirConexaoEmMemoria()
    {
        var conexao = new SqliteConnection("DataSource=:memory:");
        conexao.Open();
        return conexao;
    }

    private static DevNotesDbContext CriarContexto(SqliteConnection conexao)
    {
        var opcoes = new DbContextOptionsBuilder<DevNotesDbContext>()
            .UseSqlite(conexao)
            .Options;
        var db = new DevNotesDbContext(opcoes);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(Categoria Categoria, Tecnologia Tecnologia1, Tecnologia Tecnologia2)> SemearClassificacoesAsync(DevNotesDbContext db)
    {
        var categoria = new Categoria { Nome = "Desenvolvimento Web" };
        var tecnologias = new[] { new Tecnologia { Nome = "C#" }, new Tecnologia { Nome = "ASP.NET Core" } };
        db.Add(categoria);
        db.AddRange(tecnologias);
        await db.SaveChangesAsync();
        return (categoria, tecnologias[0], tecnologias[1]);
    }

    [Fact]
    public async Task CA_01_CriaNotaValidaComRelacoesComoRascunho()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, tecnologia1, tecnologia2) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);
        var nota = await servico.CriarAsync(new NotaEntrada(
            "Caching em ASP.NET Core",
            "## Cache\n\nBanco `IMemoryCache` aplicado.",
            categoria.Id,
            [tecnologia1.Id, tecnologia2.Id]));

        Assert.Equal(NotaEstado.Rascunho, nota.Estado);
        Assert.Equal(categoria.Id, nota.CategoriaId);
        Assert.Equal(new[] { tecnologia1.Id, tecnologia2.Id }, nota.Tecnologias.Select(t => t.TecnologiaId));
        Assert.Equal(nota.DataCriacao, nota.DataAtualizacao);

        using var dbLeitura = CriarContexto(conexao);
        var recuperada = await dbLeitura.Notas
            .Include(n => n.Categoria)
            .Include(n => n.Tecnologias)
            .ThenInclude(nt => nt.Tecnologia)
            .SingleAsync(n => n.Id == nota.Id);

        Assert.Equal("Caching em ASP.NET Core", recuperada.Titulo);
        Assert.Equal("Desenvolvimento Web", recuperada.Categoria.Nome);
        Assert.Equal(new[] { "C#", "ASP.NET Core" }, recuperada.Tecnologias.Select(t => t.Tecnologia!.Nome));
    }

    [Fact]
    public async Task CA_02_RejeitaTituloVazioSemGravar()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, _, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);

        await Assert.ThrowsAnyAsync<NoteInvalidaException>(() => servico.CriarAsync(
            new NotaEntrada("   ", "## Conteúdo válido.", categoria.Id, [1])));

        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_02_RejeitaTituloCom201CaracteresSemGravar()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, _, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);

        await Assert.ThrowsAnyAsync<NoteInvalidaException>(() => servico.CriarAsync(
            new NotaEntrada(new string('x', 201), "## Conteúdo válido.", categoria.Id, [1])));

        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CA_03_RejeitaConteudoEmBrancoSemGravar(string conteudo)
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, _, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);

        await Assert.ThrowsAnyAsync<NoteInvalidaException>(() => servico.CriarAsync(
            new NotaEntrada("Título válido", conteudo, categoria.Id, [1])));

        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_04_RejeitaSemCategoriaSemGravar()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (_, _, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);

        await Assert.ThrowsAnyAsync<NoteInvalidaException>(() => servico.CriarAsync(
            new NotaEntrada("Título válido", "## Conteúdo válido.", CategoriaId: 0, TecnologiasIds: [1])));

        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_04_RejeitaSemTecnologiaSemGravar()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, _, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);

        await Assert.ThrowsAnyAsync<NoteInvalidaException>(() => servico.CriarAsync(
            new NotaEntrada("Título válido", "## Conteúdo válido.", categoria.Id, TecnologiasIds: [])));

        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_04_RejeitaCategoriaInexistenteSemGravar()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, _, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);
        var idInexistente = categoria.Id + 99;

        await Assert.ThrowsAnyAsync<NoteInvalidaException>(() => servico.CriarAsync(
            new NotaEntrada("Título válido", "## Conteúdo válido.", idInexistente, [1])));

        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_05_AceitaTituloDuplicado()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, tecnologia, _) = await SemearClassificacoesAsync(db);

        var servico = new NoteService(db);
        await servico.CriarAsync(new NotaEntrada("Caching em ASP.NET Core", "## Primeiro conteúdo.", categoria.Id, [tecnologia.Id]));
        await servico.CriarAsync(new NotaEntrada("Caching em ASP.NET Core", "## Segundo conteúdo.", categoria.Id, [tecnologia.Id]));

        Assert.Equal(2, await db.Notas.CountAsync(n => n.Titulo == "Caching em ASP.NET Core"));
    }

    [Fact]
    public async Task ListagemOrdenaPorAtualizacaoMaisRecente()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, tecnologia, _) = await SemearClassificacoesAsync(db);

        var maisAntiga = new Nota
        {
            Titulo = "Rascunho antigo",
            Conteudo = "Conteúdo antigo.",
            Estado = NotaEstado.Rascunho,
            DataCriacao = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DataAtualizacao = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Tecnologias = []
        };
        var maisRecente = new Nota
        {
            Titulo = "Nota recente",
            Conteudo = "Conteúdo recente.",
            Estado = NotaEstado.Publicada,
            DataCriacao = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            DataAtualizacao = new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc),
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Tecnologias = []
        };
        maisAntiga.Tecnologias.Add(new NotaTecnologia { Nota = maisAntiga, Tecnologia = tecnologia });
        maisRecente.Tecnologias.Add(new NotaTecnologia { Nota = maisRecente, Tecnologia = tecnologia });
        db.AddRange(maisAntiga, maisRecente);
        await db.SaveChangesAsync();

        var listagem = await new NoteService(db).ListarAsync();

        Assert.Equal(["Nota recente", "Rascunho antigo"], listagem.Select(n => n.Titulo));
    }

    [Fact]
    public async Task ListagemProjetaCategoriaTecnologiasEstadoEResumo()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);
        var (categoria, tecnologia1, tecnologia2) = await SemearClassificacoesAsync(db);

        var nota = new Nota
        {
            Titulo = "Nota com resumo longo",
            Conteudo = new string('a', 180),
            Estado = NotaEstado.Publicada,
            DataCriacao = DateTime.UtcNow,
            DataAtualizacao = DateTime.UtcNow,
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Tecnologias = []
        };
        nota.Tecnologias.Add(new NotaTecnologia { Nota = nota, Tecnologia = tecnologia1 });
        nota.Tecnologias.Add(new NotaTecnologia { Nota = nota, Tecnologia = tecnologia2 });
        db.Add(nota);
        await db.SaveChangesAsync();

        var listagem = (await new NoteService(db).ListarAsync()).Single();

        Assert.Equal("Desenvolvimento Web", listagem.CategoriaNome);
        Assert.Equal(new[] { "C#", "ASP.NET Core" }, listagem.TecnologiasNomes);
        Assert.Equal(NotaEstado.Publicada, listagem.Estado);
        Assert.Equal(160, listagem.Resumo.Length);
    }
}
