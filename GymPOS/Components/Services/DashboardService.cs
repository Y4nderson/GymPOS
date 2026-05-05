using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class DashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        private string NombreProducto(string tipo, string marca, string descripcion, string? capacidad)
            => $"{tipo} {marca} {descripcion} {capacidad}".Trim();

        public async Task<decimal> GetVentasTotalesHoyAsync()
        {
            var hoy = DateTime.Now.Date;
            return await _context.Ventas
                .Where(v => v.Fecha.Date == hoy)
                .SumAsync(v => v.Total);
        }

        public async Task<int> GetProductosVendidosHoyAsync()
        {
            var hoy = DateTime.Now.Date;
            return await _context.DetallesVenta
                .Where(d => d.Venta.Fecha.Date == hoy)
                .SumAsync(d => d.Cantidad);
        }

        public async Task<decimal> GetVentasTotalesMesAsync()
        {
            var inicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return await _context.Ventas
                .Where(v => v.Fecha >= inicio)
                .SumAsync(v => v.Total);
        }

        public async Task<decimal> GetVentasMesAnteriorAsync()
        {
            var mesAnterior = DateTime.Now.AddMonths(-1);
            var inicio = new DateTime(mesAnterior.Year, mesAnterior.Month, 1);
            var fin = inicio.AddMonths(1);
            return await _context.Ventas
                .Where(v => v.Fecha >= inicio && v.Fecha < fin)
                .SumAsync(v => v.Total);
        }

        public async Task<string> GetProductoMasVendidoHoyAsync()
        {
            var hoy = DateTime.Now.Date;
            var detalles = await _context.DetallesVenta
                .Include(d => d.Producto)
                .Where(d => d.Venta.Fecha.Date == hoy)
                .ToListAsync();

            if (!detalles.Any()) return "Sin ventas";

            var top = detalles
                .GroupBy(d => d.ProductoId)
                .Select(g => new
                {
                    Nombre = NombreProducto(
                        g.First().Producto?.Tipo ?? "",
                        g.First().Producto?.Marca ?? "",
                        g.First().Producto?.Descripcion ?? "",
                        g.First().Producto?.Capacidad),
                    Cantidad = g.Sum(d => d.Cantidad)
                })
                .OrderByDescending(x => x.Cantidad)
                .FirstOrDefault();

            return top?.Nombre ?? "Sin ventas";
        }

        public async Task<int> GetProductosBajoStockAsync()
        {
            return await _context.Productos
                .Where(p => p.Activo && p.Stock <= p.StockMinimo)
                .CountAsync();
        }

        public async Task<List<(string Nombre, int Cantidad, decimal Total)>> GetTop5MasVendidosMesAsync()
        {
            var inicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var detalles = await _context.DetallesVenta
                .Include(d => d.Producto)
                .Where(d => d.Venta.Fecha >= inicio)
                .ToListAsync();

            return detalles
                .GroupBy(d => d.ProductoId)
                .Select(g => (
                    Nombre: NombreProducto(
                        g.First().Producto?.Tipo ?? "",
                        g.First().Producto?.Marca ?? "",
                        g.First().Producto?.Descripcion ?? "",
                        g.First().Producto?.Capacidad),
                    Cantidad: g.Sum(d => d.Cantidad),
                    Total: g.Sum(d => d.Cantidad * d.PrecioUnitario)
                ))
                .OrderByDescending(x => x.Cantidad)
                .Take(5)
                .ToList();
        }

        public async Task<List<(string Nombre, int Cantidad)>> GetTop5MenosVendidosMesAsync()
        {
            var inicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var detalles = await _context.DetallesVenta
                .Include(d => d.Producto)
                .Where(d => d.Venta.Fecha >= inicio)
                .ToListAsync();

            return detalles
                .GroupBy(d => d.ProductoId)
                .Select(g => (
                    Nombre: NombreProducto(
                        g.First().Producto?.Tipo ?? "",
                        g.First().Producto?.Marca ?? "",
                        g.First().Producto?.Descripcion ?? "",
                        g.First().Producto?.Capacidad),
                    Cantidad: g.Sum(d => d.Cantidad)
                ))
                .OrderBy(x => x.Cantidad)
                .Take(5)
                .ToList();
        }

        public async Task<Dictionary<string, decimal>> GetVentasPorMetodoMesAsync()
        {
            var inicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var result = await _context.Ventas
                .Where(v => v.Fecha >= inicio)
                .GroupBy(v => v.MetodoPago)
                .Select(g => new { Metodo = g.Key, Total = g.Sum(v => v.Total) })
                .ToListAsync();
            return result.ToDictionary(x => x.Metodo, x => x.Total);
        }

        public async Task<List<(string Dia, decimal Total)>> GetVentasUltimos7DiasAsync()
        {
            var inicio = DateTime.Now.Date.AddDays(-6);
            var ventas = await _context.Ventas
                .Where(v => v.Fecha.Date >= inicio)
                .GroupBy(v => v.Fecha.Date)
                .Select(g => new { Dia = g.Key, Total = g.Sum(v => v.Total) })
                .OrderBy(x => x.Dia)
                .ToListAsync();

            var resultado = new List<(string, decimal)>();
            for (int i = 6; i >= 0; i--)
            {
                var dia = DateTime.Now.Date.AddDays(-i);
                var venta = ventas.FirstOrDefault(v => v.Dia == dia);
                resultado.Add((dia.ToString("ddd dd"), venta?.Total ?? 0));
            }
            return resultado;
        }

        public async Task<List<Producto>> GetProductosBajoStockListAsync()
        {
            return await _context.Productos
                .Where(p => p.Activo && p.Stock <= p.StockMinimo)
                .OrderBy(p => p.Stock)
                .Take(5)
                .ToListAsync();
        }

        public async Task<bool> CajaAbiertaAsync()
        {
            var hoy = DateTime.Now.Date;
            var apertura = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy && m.Tipo == "Apertura")
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();
            if (apertura == null) return false;
            return !await _context.MovimientosCaja
                .AnyAsync(m => m.Fecha.Date == hoy && m.Tipo == "CierreDefinitivo" && m.Fecha > apertura.Fecha);
        }
    }
}