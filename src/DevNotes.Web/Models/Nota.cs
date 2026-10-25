namespace DevNotes.Web.Models;

public class Nota
{
    public int Id { get; set; }
    public required string Titulo { get; set; }
    public required string Conteudo { get; set; }
    public required NotaEstado Estado { get; set; }
    public required DateTime DataCriacao { get; set; }
    public required DateTime DataAtualizacao { get; set; }
    public required int CategoriaId { get; set; }
    public required Categoria Categoria { get; set; }
    public required ICollection<NotaTecnologia> Tecnologias { get; set; }
}

public class NotaTecnologia
{
    public int NotaId { get; set; }
    public required Nota Nota { get; set; }
    public int TecnologiaId { get; set; }
    public required Tecnologia Tecnologia { get; set; }
}
