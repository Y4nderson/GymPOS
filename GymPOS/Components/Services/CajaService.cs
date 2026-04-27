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

        public async Task<List<MovimientoCaja>> GetMovimientosHoyAsync()
        {
            var hoy = DateTime.Today;
            return await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy)
                .OrderBy(m => m.Fecha)
                .ToListAsync();
        }

        public async Task AbrirCajaAsync(decimal montoInicial, string descripcion)
        {
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
            var hoy = DateTime.Today;
            return await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy
                         && m.Tipo == "Venta"
                         && m.MetodoPago == metodoPago)
                .SumAsync(m => m.Monto);
        }

        public async Task<decimal> GetTotalVentasHoyAsync()
        {
            var hoy = DateTime.Today;
            return await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy && m.Tipo == "Venta")
                .SumAsync(m => m.Monto);
        }

        public async Task<decimal> GetTotalEgresosHoyAsync()
        {
            var hoy = DateTime.Today;
            return Math.Abs(await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy && m.Tipo == "Egreso")
                .SumAsync(m => m.Monto));
        }

        // Registrar corte sin cerrar la caja
        public async Task RegistrarCorteAsync(string turno, decimal montoContado)
        {
            var turnoId = await GetTurnoActivoIdAsync();
            var hoy = DateTime.Today;

            // Efectivo esperado = monto inicial + ventas efectivo - egresos
            var apertura = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy && m.Tipo == "Apertura")
                .FirstOrDefaultAsync();
            var montoInicial = apertura?.Monto ?? 0;
            var ventasEfectivo = await GetTotalPorMetodoAsync("Efectivo");
            var egresos = await GetTotalEgresosHoyAsync();
            var efectivoEsperado = montoInicial + ventasEfectivo - egresos;
            var diferencia = montoContado - efectivoEsperado;

            _context.MovimientosCaja.Add(new MovimientoCaja
            {
                Tipo = "Corte",
                Descripcion = $"Corte {turno}. Efectivo esperado: ₡{efectivoEsperado:N0}. Contado: ₡{montoContado:N0}. Diferencia: ₡{diferencia:N0}",
                Monto = montoContado,
                MetodoPago = "Efectivo",
                TurnoId = turnoId
            });
            await _context.SaveChangesAsync();
        }

        // Solo el cierre definitivo cierra la caja
        public async Task CerrarCajaAsync(decimal montoFinal)
        {
            var turnoId = await GetTurnoActivoIdAsync();
            var totalEfectivo = await GetTotalPorMetodoAsync("Efectivo");
            var apertura = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == DateTime.Today && m.Tipo == "Apertura")
                .FirstOrDefaultAsync();
            var montoInicial = apertura?.Monto ?? 0;
            var diferencia = montoFinal - (montoInicial + totalEfectivo);

            _context.MovimientosCaja.Add(new MovimientoCaja
            {
                Tipo = "CierreDefinitivo",
                Descripcion = $"Cierre definitivo del día. Diferencia: ₡{diferencia:N0}",
                Monto = montoFinal,
                MetodoPago = "Efectivo",
                TurnoId = turnoId
            });
            await _context.SaveChangesAsync();
        }

        // Caja abierta = hay apertura hoy sin cierre definitivo
        public async Task<bool> CajaAbiertaHoyAsync()
        {
            var hoy = DateTime.Now.Date;

            var apertura = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy && m.Tipo == "Apertura")
                .FirstOrDefaultAsync();

            if (apertura == null) return false;

            var cierreDefinitivo = await _context.MovimientosCaja
                .AnyAsync(m => m.Fecha.Date == hoy && m.Tipo == "CierreDefinitivo");

            return !cierreDefinitivo;
        }

        public async Task<bool> CajaCerradaDefinitivamenteHoyAsync()
        {
            var hoy = DateTime.Today;
            return await _context.MovimientosCaja
                .AnyAsync(m => m.Fecha.Date == hoy && m.Tipo == "CierreDefinitivo");
        }
    }
}