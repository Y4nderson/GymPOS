using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace GymPOS.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public async Task<Usuario?> LoginAsync(string username, string password)
        {
            var hash = HashPassword(password);
            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Username == username
                                       && u.PasswordHash == hash
                                       && u.Activo);
        }

        public async Task<List<Usuario>> GetAllAsync()
            => await _context.Usuarios.OrderBy(u => u.Nombre).ToListAsync();

        public async Task CrearAsync(Usuario usuario, string password)
        {
            usuario.PasswordHash = HashPassword(password);
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
        }

        public async Task ActualizarAsync(Usuario usuario, string? nuevaPassword = null)
        {
            if (!string.IsNullOrEmpty(nuevaPassword))
                usuario.PasswordHash = HashPassword(nuevaPassword);
            _context.Usuarios.Update(usuario);
            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var u = await _context.Usuarios.FindAsync(id);
            if (u != null)
            {
                u.Activo = false;
                await _context.SaveChangesAsync();
            }
        }

        public async Task SeedAdminAsync()
        {
            if (!await _context.Usuarios.AnyAsync())
            {
                _context.Usuarios.Add(new Usuario
                {
                    Nombre = "Administrador",
                    Username = "admin",
                    PasswordHash = HashPassword("Admin123!"),
                    Rol = "Admin",
                    Activo = true
                });
                await _context.SaveChangesAsync();
            }
        }
    }
}