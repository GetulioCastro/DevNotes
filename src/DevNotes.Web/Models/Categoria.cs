namespace DevNotes.Web.Models;

public class Categoria
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public List<Nota> Notas { get; set; } = [];
}
