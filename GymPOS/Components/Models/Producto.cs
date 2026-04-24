using System.ComponentModel.DataAnnotations.Schema;

namespace GymPOS.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string? Capacidad { get; set; }
        public string? CodigoBarra { get; set; }
        public string Nombre => $"{Tipo} {Marca} {Descripcion} {Capacidad}".Trim();

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public string? ImagenPath { get; set; }
        public bool Activo { get; set; } = true;
    }
}