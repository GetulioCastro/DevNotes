using DevNotes.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNotes.Web.Data;

public static class DevNotesSeeder
{
    // Seed de demonstração: cria cada tipo apenas quando a tabela está vazia,
    // para que reaplicações não dupliquem dados. Sem notas (RN-07).
    public static async Task SeedAsync(DevNotesDbContext db, CancellationToken ct = default)
    {
        var alterou = false;

        if (!await db.Categorias.AnyAsync(ct))
        {
            db.Categorias.AddRange(
                new Categoria { Nome = "Desenvolvimento Web" },
                new Categoria { Nome = "Banco de Dados" });
            alterou = true;
        }

        if (!await db.Tecnologias.AnyAsync(ct))
        {
            db.Tecnologias.AddRange(
                new Tecnologia { Nome = "C#" },
                new Tecnologia { Nome = "ASP.NET Core" },
                new Tecnologia { Nome = "ADO.NET" });
            alterou = true;
        }

        if (alterou)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
