using System.ComponentModel.DataAnnotations.Schema;

namespace GymPOS.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";
        public int TurnoId { get; set; }
        public List<DetalleVenta> Detalles { get; set; } = new();
    }
}