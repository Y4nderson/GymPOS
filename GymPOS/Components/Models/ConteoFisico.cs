namespace GymPOS.Models
{
    public class ConteoFisico
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }
        public int CantidadContada { get; set; }
        public int CantidadSistema { get; set; }
        public int Diferencia => CantidadContada - CantidadSistema;
        public int TurnoId { get; set; }
        public string Turno { get; set; } = string.Empty; // "Mediodía" o "Noche"
    }
}