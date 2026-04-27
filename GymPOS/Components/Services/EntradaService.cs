using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class EntradaService
    {
        private readonly AppDbContext _context;
        private readonly CajaService _cajaService;

        public EntradaService(AppDbContext context, CajaService cajaService)
        {
            _context = context;
            _cajaService = cajaService;
        }

        public async Task<List<EntradaProducto>> GetEntradasHoyAsync()
        {
            var hoy = DateTime.Today;
            return await _context.EntradasProducto
                .Include(e => e.Producto)
                .Where(e => e.Fecha.Date == hoy)
                .OrderByDescending(e => e.Fecha)
                .ToListAsync();
        }

        public async Task RegistrarEntradaAsync(EntradaProducto entrada)
        {
            entrada.TurnoId = await _cajaService.GetTurnoActivoIdAsync();

            var producto = await _context.Productos.FindAsync(entrada.ProductoId);
            if (producto != null)
                producto.Stock += entrada.Cantidad;

            // Registrar pago de factura como egreso en caja si hay costo
            if (entrada.CostoFactura > 0)
            {
                _context.MovimientosCaja.Add(new MovimientoCaja
                {
                    Tipo = "Egreso",
                    Descripcion = $"Pago factura: {entrada.Proveedor ?? "Proveedor"} - {producto?.Nombre}",
                    Monto = -entrada.CostoFactura,
                    MetodoPago = "Efectivo",
                    TurnoId = entrada.TurnoId
                });
            }

            _context.EntradasProducto.Add(entrada);
            await _context.SaveChangesAsync();
        }
    }
}