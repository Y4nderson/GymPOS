using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class ConteoService
    {
        private readonly AppDbContext _context;
        private readonly CajaService _cajaService;

        public ConteoService(AppDbContext context, CajaService cajaService)
        {
            _context = context;
            _cajaService = cajaService;
        }

        public async Task<List<ConteoFisico>> GetConteosDelTurnoAsync()
        {
            var turnoId = await _cajaService.GetTurnoActivoIdAsync();
            return await _context.ContesFisicos
                .Include(c => c.Producto)
                .Where(c => c.TurnoId == turnoId)
                .ToListAsync();
        }

        public async Task GuardarConteoAsync(List<ConteoFisico> conteos, string turno)
        {
            var turnoId = await _cajaService.GetTurnoActivoIdAsync();
            foreach (var conteo in conteos)
            {
                conteo.TurnoId = turnoId;
                conteo.Turno = turno;
                conteo.Fecha = DateTime.Now;
                _context.ContesFisicos.Add(conteo);
            }
            await _context.SaveChangesAsync();
        }

        // Calcula total vendido según conteo físico
        public async Task<decimal> GetTotalVentasPorConteoAsync()
        {
            var turnoId = await _cajaService.GetTurnoActivoIdAsync();

            // Solo tomar conteos que NO sean de Mañana
            var conteos = await _context.ContesFisicos
                .Include(c => c.Producto)
                .Where(c => c.TurnoId == turnoId && c.Turno != "Mañana")
                .ToListAsync();

            return conteos
                .Where(c => c.Diferencia < 0)
                .Sum(c => Math.Abs(c.Diferencia) * (c.Producto?.Precio ?? 0));
        }
    }
}