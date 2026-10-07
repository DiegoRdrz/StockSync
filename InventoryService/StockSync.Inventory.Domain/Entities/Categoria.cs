using StockSync.Inventory.Domain.Common;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.Domain.Entities;

public class Categoria
{
    public const int NombreMaxLength = 100;
    public const int DescripcionMaxLength = 300;

    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    // Se persiste para que el índice único de la base aplique la misma comparación que la aplicación.
    public string NombreNormalizado { get; private set; } = null!;
    public string? Descripcion { get; private set; }
    public bool Activo { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaActualizacion { get; private set; }

    private Categoria()
    {
    }

    public static Categoria Crear(string nombre, string? descripcion)
    {
        var categoria = new Categoria
        {
            Id = Guid.NewGuid(),
            Activo = true,
            FechaCreacion = RelojUtc.Ahora()
        };
        categoria.EstablecerDatos(nombre, descripcion);
        return categoria;
    }

    public void Actualizar(string nombre, string? descripcion)
    {
        AsegurarActivo();
        EstablecerDatos(nombre, descripcion);
        FechaActualizacion = RelojUtc.Ahora();
    }

    public void Desactivar()
    {
        AsegurarActivo();
        Activo = false;
        FechaActualizacion = RelojUtc.Ahora();
    }

    // Se usa para comparar unicidad: "Herramientas" y "  herramientas " se consideran la misma categoría.
    public static string NormalizarNombre(string nombre) => nombre.Trim().ToUpperInvariant();

    private void EstablecerDatos(string nombre, string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre de la categoría es obligatorio.");
        if (nombre.Trim().Length > NombreMaxLength)
            throw new DomainException($"El nombre no puede superar {NombreMaxLength} caracteres.");
        if (descripcion?.Trim().Length > DescripcionMaxLength)
            throw new DomainException($"La descripción no puede superar {DescripcionMaxLength} caracteres.");

        Nombre = nombre.Trim();
        NombreNormalizado = NormalizarNombre(nombre);
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
    }

    private void AsegurarActivo()
    {
        if (!Activo)
            throw new DomainException("La categoría está dada de baja.");
    }
}
