namespace DevNotes.Web.Models;

public class Tecnologia
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public List<NotaTecnologia> Notas { get; set; } = [];
}
