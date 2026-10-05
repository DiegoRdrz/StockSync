using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class CategoriaTests
{
    [Fact]
    public void Crear_ConDatosValidos_InicializaActiva()
    {
        var categoria = Categoria.Crear("Herramientas", "Herramientas de uso general");

        Assert.NotEqual(Guid.Empty, categoria.Id);
        Assert.True(categoria.Activo);
        Assert.Equal("Herramientas", categoria.Nombre);
        Assert.Equal("Herramientas de uso general", categoria.Descripcion);
        Assert.Null(categoria.FechaActualizacion);
    }

    [Fact]
    public void Crear_NormalizaDatosYTextosVaciosSonNulos()
    {
        var categoria = Categoria.Crear(" Herramientas ", "   ");

        Assert.Equal("Herramientas", categoria.Nombre);
        Assert.Null(categoria.Descripcion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinNombre_LanzaDomainException(string nombre)
    {
        Assert.Throws<DomainException>(() => Categoria.Crear(nombre, null));
    }

    [Fact]
    public void Crear_ConTextosDemasiadoLargos_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => Categoria.Crear(new string('a', Categoria.NombreMaxLength + 1), null));
        Assert.Throws<DomainException>(() => Categoria.Crear("Valid", new string('a', Categoria.DescripcionMaxLength + 1)));
    }

    [Fact]
    public void Actualizar_ModificaDatosYFechaActualizacion()
    {
        var categoria = Categoria.Crear("Herramientas", null);

        categoria.Actualizar("Otras herramientas", "Nueva descripción");

        Assert.Equal("Otras herramientas", categoria.Nombre);
        Assert.Equal("Nueva descripción", categoria.Descripcion);
        Assert.NotNull(categoria.FechaActualizacion);
    }

    [Fact]
    public void Desactivar_MarcaInactivaYActualizaFecha()
    {
        var categoria = Categoria.Crear("Test", null);

        categoria.Desactivar();

        Assert.False(categoria.Activo);
        Assert.NotNull(categoria.FechaActualizacion);
    }

    [Fact]
    public void ActualizarODesactivar_Inactiva_LanzaDomainException()
    {
        var categoria = Categoria.Crear("Test", null);
        categoria.Desactivar();

        Assert.Throws<DomainException>(() => categoria.Actualizar("Test2", null));
        Assert.Throws<DomainException>(categoria.Desactivar);
    }
}
