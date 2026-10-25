using DevNotes.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevNotes.Tests;

public class MigracaoTests
{
    private static string CaminhoTemporario()
    {
        return Path.Combine(Path.GetTempPath(), $"devnotes-migrations-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task MigracaoAplicaSeESchemaTemTabelasIndicesEFksSemCascata()
    {
        var caminho = CaminhoTemporario();
        try
        {
            var opcoes = new DbContextOptionsBuilder<DevNotesDbContext>()
                .UseSqlite($"Data Source={caminho}")
                .Options;

            using (var db = new DevNotesDbContext(opcoes))
            {
                await db.Database.MigrateAsync();
            }

            await using (var conexao = new SqliteConnection($"Data Source={caminho}"))
            {
                await conexao.OpenAsync();

                using (var comando = conexao.CreateCommand())
                {
                    comando.CommandText =
                        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Notas','Categorias','Tecnologias','NotasTecnologias')";
                    var tabelas = (long)(await comando.ExecuteScalarAsync())!;
                    Assert.Equal(4, tabelas);
                }

                using (var comando = conexao.CreateCommand())
                {
                    comando.CommandText =
                        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name IN ('IX_Notas_Titulo','IX_Notas_Estado','IX_NotasTecnologias_TecnologiaId')";
                    var indices = (long)(await comando.ExecuteScalarAsync())!;
                    Assert.Equal(3, indices);
                }

                // FKs sem cascata: o DDL das tabelas deve definir ON DELETE RESTRICT
                using (var comando = conexao.CreateCommand())
                {
                    comando.CommandText =
                        "SELECT sql FROM sqlite_master WHERE type = 'table' AND name IN ('Notas','NotasTecnologias')";
                    var leitor = await comando.ExecuteReaderAsync();
                    var ddl = new System.Text.StringBuilder();

                    while (await leitor.ReadAsync())
                    {
                        ddl.AppendLine(leitor.GetString(0));
                    }

                    var schema = ddl.ToString();
                    Assert.Equal(3, schema.Split("ON DELETE RESTRICT", StringSplitOptions.None).Length - 1);
                    Assert.DoesNotContain("ON DELETE CASCADE", schema);
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(caminho);
            }
            catch (IOException)
            {
                // Arquivo temporário: exclusão residual não falha o teste
            }
        }
    }
}
