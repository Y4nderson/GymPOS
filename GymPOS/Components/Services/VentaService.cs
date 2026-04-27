using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class VentaService
    {
        private readonly AppDbContext _context;
        private readonly CajaService _cajaService;

        public VentaService(AppDbContext context, CajaService cajaService)
        {
            _context = context;
            _cajaService = cajaService;
        }

        public async Task<List<Venta>> GetAllAsync()
            => await _context.Ventas
                .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
                .OrderByDescending(v => v.Fecha)
                .ToListAsync();

        public async Task<Venta> CrearVentaAsync(Venta venta)
        {
            var turnoId = await _cajaService.GetTurnoActivoIdAsync();
            venta.TurnoId = turnoId;

            foreach (var detalle in venta.Detalles)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null)
                {
                    producto.Stock -= detalle.Cantidad;
                    // Nunca permitir stock negativo
                    if (producto.Stock < 0) producto.Stock = 0;
                }
            }

            _context.MovimientosCaja.Add(new MovimientoCaja
            {
                Tipo = "Venta",
                Descripcion = $"Venta - {venta.Detalles.Count} producto(s)",
                Monto = venta.Total,
                MetodoPago = venta.MetodoPago,
                TurnoId = turnoId
            });

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();
            return venta;
        }
    }
}