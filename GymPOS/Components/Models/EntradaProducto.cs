using System.ComponentModel.DataAnnotations.Schema;

namespace GymPOS.Models
{
    public class EntradaProducto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }
        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoFactura { get; set; } // Lo que se pagó al proveedor

        public string? Proveedor { get; set; }
        public string? Observacion { get; set; }
        public int TurnoId { get; set; }
    }
}