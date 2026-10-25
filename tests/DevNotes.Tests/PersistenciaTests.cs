using DevNotes.Web.Data;
using DevNotes.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevNotes.Tests;

public class PersistenciaTests
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

    private static Nota CriarNota(Categoria categoria, params Tecnologia[] tecnologias)
    {
        var agora = DateTime.UtcNow;

        var nota = new Nota
        {
            Titulo = "Caching em ASP.NET Core",
            Conteudo = "## Cache\n\nBanco `IMemoryCache` aplicado.",
            Estado = NotaEstado.Rascunho,
            DataCriacao = agora,
            DataAtualizacao = agora,
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Tecnologias = []
        };

        foreach (var tecnologia in tecnologias)
        {
            nota.Tecnologias.Add(new NotaTecnologia { Nota = nota, Tecnologia = tecnologia });
        }

        return nota;
    }

    [Fact]
    public async Task SalvarERecuperarNotaComCategoriaETecnologias()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);

        var categoria = new Categoria { Nome = "Desenvolvimento Web" };
        var tecnologia = new Tecnologia { Nome = "ASP.NET Core" };
        db.AddRange(categoria, tecnologia);
        await db.SaveChangesAsync();

        var nota = CriarNota(categoria, tecnologia);
        db.Notas.Add(nota);
        await db.SaveChangesAsync(); // nota + classificações na mesma transação

        using var dbLeitura = CriarContexto(conexao);
        var recuperada = await dbLeitura.Notas
            .Include(n => n.Categoria)
            .Include(n => n.Tecnologias)
            .ThenInclude(nt => nt.Tecnologia)
            .SingleAsync(n => n.Id == nota.Id);

        Assert.Equal(nota.Titulo, recuperada.Titulo);
        Assert.Equal(NotaEstado.Rascunho, recuperada.Estado);
        Assert.Equal("Desenvolvimento Web", recuperada.Categoria.Nome);
        var vinculada = Assert.Single(recuperada.Tecnologias);
        Assert.Equal("ASP.NET Core", vinculada.Tecnologia.Nome);
        Assert.Equal(recuperada.DataCriacao, recuperada.DataAtualizacao);
    }

    [Fact]
    public async Task TecnologiaCompartilhadaEntreDuasNotas()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);

        var categoria = new Categoria { Nome = "Banco de Dados" };
        var tecnologia = new Tecnologia { Nome = "ADO.NET" };
        db.AddRange(categoria, tecnologia);
        await db.SaveChangesAsync();

        db.AddRange(
            CriarNota(categoria, tecnologia),
            CriarNota(categoria, tecnologia));
        await db.SaveChangesAsync();

        using var dbLeitura = CriarContexto(conexao);
        var notaId = await dbLeitura.Notas.Select(n => n.Id).MinAsync();
        var recuperada = await dbLeitura.Notas
            .Include(n => n.Tecnologias)
            .ThenInclude(nt => nt.Tecnologia)
            .SingleAsync(n => n.Id == notaId);

        var vinculada = Assert.Single(recuperada.Tecnologias);
        Assert.Equal("ADO.NET", vinculada.Tecnologia.Nome);
        Assert.Equal(2, await dbLeitura.Set<NotaTecnologia>().CountAsync(nt => nt.TecnologiaId == tecnologia.Id));
    }

    [Fact]
    public async Task ExcluirCategoriaEmUsoEBloqueadaSemCascata()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);

        var categoria = new Categoria { Nome = "IA Agêntica" };
        db.Add(categoria);
        await db.SaveChangesAsync();
        db.Add(CriarNota(categoria));
        await db.SaveChangesAsync();

        // Exclusão em contexto sem a nota rastreada: chega à restrição FK do banco (sem cascade)
        using var dbExclusao = CriarContexto(conexao);
        dbExclusao.Remove(new Categoria { Id = categoria.Id, Nome = categoria.Nome });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbExclusao.SaveChangesAsync());

        using var dbVerificacao = CriarContexto(conexao);
        Assert.Equal(1, await dbVerificacao.Notas.CountAsync(n => n.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task SeedReaplicadaNaoDuplicaClassificacoes()
    {
        await using var conexao = AbrirConexaoEmMemoria();
        var db = CriarContexto(conexao);

        await DevNotesSeeder.SeedAsync(db);
        await DevNotesSeeder.SeedAsync(db);

        Assert.Equal(2, await db.Categorias.CountAsync());
        Assert.Equal(3, await db.Tecnologias.CountAsync());
        Assert.Equal(0, await db.Notas.CountAsync());
    }
}
