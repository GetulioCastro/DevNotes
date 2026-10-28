using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using DevNotes.Web.Data;
using DevNotes.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevNotes.Tests;

public class CriarNotaWebTests
{
    private static WebApplicationFactory<Program> CriarFabrica()
    {
        var caminhoBanco = Path.Combine(Path.GetTempPath(), $"devnotes-web-{Guid.NewGuid():N}.db");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DevNotes"] = $"Data Source={caminhoBanco}"
                })));
    }

    private static async Task<string> ObterTokenAntiforgeryAsync(HttpClient cliente)
    {
        var html = await cliente.GetStringAsync("/Admin/Notas/Criar");
        return Regex.Match(html, "__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value
            ?? throw new InvalidOperationException("Token antiforgery não encontrado no formulário.");
    }

    private static async Task<string> LerHtmlDecodificadoAsync(HttpResponseMessage resposta)
    {
        return WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());
    }

    private static List<KeyValuePair<string, string>> Formulario(
        string token, string titulo, string conteudo, string categoriaId, params string[] tecnologiasIds)
    {
        var campos = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Entrada.Titulo", titulo),
            new("Entrada.Conteudo", conteudo),
            new("Entrada.CategoriaId", categoriaId)
        };
        campos.AddRange(tecnologiasIds.Select(id => new KeyValuePair<string, string>("Entrada.TecnologiasIds", id)));
        return campos;
    }

    private static async Task<(int CategoriaId, int Tecnologia1Id, int Tecnologia2Id)> ObterIdsSeedAsync(WebApplicationFactory<Program> fabrica)
    {
        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        var categoria = await db.Categorias.OrderBy(c => c.Id).FirstAsync();
        var tecnologias = await db.Tecnologias.OrderBy(t => t.Id).Take(2).ToListAsync();
        return (categoria.Id, tecnologias[0].Id, tecnologias[1].Id);
    }

    [Fact]
    public async Task CA_01_PostValidoCriaNotaRascunhoEApareceNaListagem()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, tecnologia2) = await ObterIdsSeedAsync(fabrica);

        var resposta = await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Caching em ASP.NET Core", "## Cache\n\nUse `IMemoryCache`.", categoriaId.ToString(), tecnologia1.ToString(), tecnologia2.ToString())));

        Assert.True(resposta.IsSuccessStatusCode);
        var listagem = await LerHtmlDecodificadoAsync(resposta);
        Assert.Contains("Caching em ASP.NET Core", listagem);
        Assert.Contains("Nota salva", listagem); // flash UI-04.sucesso → UI-03

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        var nota = await db.Notas.Include(n => n.Tecnologias).SingleAsync();
        Assert.Equal(NotaEstado.Rascunho, nota.Estado);
        Assert.Equal(2, nota.Tecnologias.Count); // N:M persistida na mesma transação
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CA_02_PostTituloVazioReRenderizaSemGravar(string titulo)
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, _) = await ObterIdsSeedAsync(fabrica);

        var resposta = await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, titulo, "## Conteúdo preservado.", categoriaId.ToString(), tecnologia1.ToString())));

        Assert.Contains("O título é obrigatório", await LerHtmlDecodificadoAsync(resposta));

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_02_PostTituloCom201CaracteresReRenderizaSemGravar()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, _) = await ObterIdsSeedAsync(fabrica);

        var resposta = await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, new string('x', 201), "## Conteúdo válido.", categoriaId.ToString(), tecnologia1.ToString())));

        Assert.Contains("no máximo 200 caracteres", await LerHtmlDecodificadoAsync(resposta));

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_03_PostConteudoEmBrancoReRenderizaPreservandoDados()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, _) = await ObterIdsSeedAsync(fabrica);

        var resposta = await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Título preservado", "   ", categoriaId.ToString(), tecnologia1.ToString())));

        var html = await LerHtmlDecodificadoAsync(resposta);
        Assert.Contains("O conteúdo em Markdown é obrigatório", html);
        Assert.Contains("value=\"Título preservado\"", html); // dados digitados preservados (UI-04.validacao)

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_04_PostSemCategoriaReRenderizaApontandoClassificacao()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, _) = await ObterIdsSeedAsync(fabrica);

        var resposta = await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Título válido", "## Conteúdo válido.", "0", tecnologia1.ToString())));

        Assert.Contains("Selecione uma categoria", await LerHtmlDecodificadoAsync(resposta));

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_04_PostSemTecnologiaReRenderizaApontandoClassificacao()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, _, _) = await ObterIdsSeedAsync(fabrica);

        var resposta = await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Título válido", "## Conteúdo válido.", categoriaId.ToString())));

        Assert.Contains("Informe pelo menos uma tecnologia", await LerHtmlDecodificadoAsync(resposta));

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        Assert.Equal(0, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task CA_05_PostTituloDuplicadoCoexiste()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, _) = await ObterIdsSeedAsync(fabrica);

        await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Mesmo título", "## Primeiro conteúdo.", categoriaId.ToString(), tecnologia1.ToString())));
        await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Mesmo título", "## Segundo conteúdo.", categoriaId.ToString(), tecnologia1.ToString())));

        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
        Assert.Equal(2, await db.Notas.CountAsync(n => n.Titulo == "Mesmo título"));
    }

    [Fact]
    public async Task CA_08_FormularioUsaTextareaSimplesComContador()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();

        var html = await cliente.GetStringAsync("/Admin/Notas/Criar");

        Assert.Contains("<textarea", html);
        Assert.Contains("font-monospace", html);           // editor monospace (RN-14/ADR-005)
        Assert.Contains("/200", html);                     // contador x/200 (RN-01)
        Assert.DoesNotContain("wysiwyg", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contenteditable", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListagemVaziaExibeEstadoVazio()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();

        var html = await cliente.GetStringAsync("/Admin/Notas");

        Assert.Contains("Nenhuma nota ainda", html);       // UI-03.vazio
        Assert.Contains("Criar primeira nota", html);
    }

    [Fact]
    public async Task ListagemOrdenaPorAtualizacaoMaisRecentePrimeiro()
    {
        using var fabrica = CriarFabrica();
        var cliente = fabrica.CreateClient();
        var token = await ObterTokenAntiforgeryAsync(cliente);
        var (categoriaId, tecnologia1, _) = await ObterIdsSeedAsync(fabrica);

        await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Primeira nota", "## Primeira.", categoriaId.ToString(), tecnologia1.ToString())));
        await cliente.PostAsync("/Admin/Notas/Criar", new FormUrlEncodedContent(Formulario(
            token, "Segunda nota", "## Segunda.", categoriaId.ToString(), tecnologia1.ToString())));

        var html = await cliente.GetStringAsync("/Admin/Notas");
        var posicaoPrimeira = html.IndexOf("Primeira nota", StringComparison.Ordinal);
        var posicaoSegunda = html.IndexOf("Segunda nota", StringComparison.Ordinal);

        Assert.True(posicaoSegunda < posicaoPrimeira, "A nota atualizada mais recentemente deve vir primeiro (RN-11).");
    }
}
