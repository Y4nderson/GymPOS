using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class InventarioService
    {
        private readonly AppDbContext _context;

        public InventarioService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Producto>> GetProductosAsync()
            => await _context.Productos.Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();

        public async Task AjustarStockAsync(int productoId, int cantidad, string motivo)
        {
            var producto = await _context.Productos.FindAsync(productoId);
            if (producto != null)
            {
                producto.Stock += cantidad;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Producto>> GetProductosBajoStockAsync()
            => await _context.Productos
                .Where(p => p.Activo && p.Stock <= p.StockMinimo)
                .ToListAsync();
    }
}