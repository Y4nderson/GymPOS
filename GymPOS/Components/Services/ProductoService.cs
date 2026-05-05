using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class ProductoService
    {
        private readonly AppDbContext _context;

        public ProductoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Producto>> GetAllAsync()
            => await _context.Productos.Where(p => p.Activo).ToListAsync();

        public async Task<Producto?> GetByIdAsync(int id)
            => await _context.Productos.FindAsync(id);

        public async Task<Producto?> GetByCodigoBarraAsync(string codigo)
            => await _context.Productos.FirstOrDefaultAsync(p => p.CodigoBarra == codigo);

        public async Task AddAsync(Producto producto)
        {
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Producto producto)
        {
            var existing = await _context.Productos.FindAsync(producto.Id);
            if (existing != null)
            {
                existing.Tipo = producto.Tipo;
                existing.Marca = producto.Marca;
                existing.Descripcion = producto.Descripcion;
                existing.Capacidad = producto.Capacidad;
                existing.CodigoBarra = producto.CodigoBarra;
                existing.Precio = producto.Precio;
                existing.StockMinimo = producto.StockMinimo;
                existing.Activo = producto.Activo;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            var producto = await GetByIdAsync(id);
            if (producto != null)
            {
                producto.Activo = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}