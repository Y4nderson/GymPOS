using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class CajaService
    {
        private readonly AppDbContext _context;

        public CajaService(AppDbContext context)
        {
            _context = context;
        }

        // Obtiene el TurnoId activo actual
        public async Task<int> GetTurnoActivoIdAsync()
        {
            var hoy = DateTime.Today;
            var apertura = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy && m.Tipo == "Apertura")
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();
            return apertura?.TurnoId ?? 0;
        }

        public async Task<List<MovimientoCaja>> GetMovimientosTurnoAsync()
        {
            var turnoId = await GetTurnoActivoIdAsync();
            return await _context.MovimientosCaja
                .Where(m => m.TurnoId == turnoId)
                .OrderBy(m => m.Fecha)
                .ToListAsync();
        }

        public async Task AbrirCajaAsync(decimal montoInicial, string descripcion)
        {
            // Genera un nuevo TurnoId único
            var nuevoTurnoId = (await _context.MovimientosCaja.AnyAsync())
                ? await _context.MovimientosCaja.MaxAsync(m => m.TurnoId) + 1
                : 1;

            _context.MovimientosCaja.Add(new MovimientoCaja
            {
                Tipo = "Apertura",
                Descripcion = descripcion,
                Monto = montoInicial,
                MetodoPago = "Efectivo",
                TurnoId = nuevoTurnoId
            });
            await _context.SaveChangesAsync();
        }

        public async Task AgregarEgresoAsync(string descripcion, decimal monto)
        {
            var turnoId = await GetTurnoActivoIdAsync();
            _context.MovimientosCaja.Add(new MovimientoCaja
            {
                Tipo = "Egreso",
                Descripcion = descripcion,
                Monto = -monto,
                MetodoPago = "Efectivo",
                TurnoId = turnoId
            });
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalPorMetodoAsync(string metodoPago)
        {
            var turnoId = await GetTurnoActivoIdAsync();
            return await _context.Ventas
                .Where(v => v.TurnoId == turnoId && v.MetodoPago == metodoPago)
                .SumAsync(v => v.Total);
        }

        public async Task<decimal> GetTotalVentasHoyAsync()
        {
            var turnoId = await GetTurnoActivoIdAsync();
            return await _context.Ventas
                .Where(v => v.TurnoId == turnoId)
                .SumAsync(v => v.Total);
        }

        public async Task CerrarCajaAsync(decimal montoFinal, bool cierreDefinitivo = false)
        {
            var turnoId = await GetTurnoActivoIdAsync();
            var totalEfectivo = await GetTotalPorMetodoAsync("Efectivo");
            var diferencia = montoFinal - totalEfectivo;
            var tipo = cierreDefinitivo ? "CierreDefinitivo" : "Cierre";

            _context.MovimientosCaja.Add(new MovimientoCaja
            {
                Tipo = tipo,
                Descripcion = $"Cierre de turno. Diferencia: ₡{diferencia:N0}",
                Monto = montoFinal,
                MetodoPago = "Efectivo",
                TurnoId = turnoId
            });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CajaAbiertaHoyAsync()
        {
            var hoy = DateTime.Today;
            var ultimo = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy)
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();
            return ultimo?.Tipo == "Apertura";
        }

        public async Task<bool> CajaCerradaDefinitivamenteHoyAsync()
        {
            var hoy = DateTime.Today;
            return await _context.MovimientosCaja
                .AnyAsync(m => m.Fecha.Date == hoy && m.Tipo == "CierreDefinitivo");
        }
    }
}