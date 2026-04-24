using System.ComponentModel.DataAnnotations.Schema;

namespace GymPOS.Models
{
    public class MovimientoCaja
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Tipo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";
        public string? Turno { get; set; }
        public int TurnoId { get; set; } // Identificador del turno actual
    }
}