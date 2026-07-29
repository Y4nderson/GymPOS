using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace GymPOS.Services
{
    public class CorreoService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public CorreoService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<bool> EnviarAlertaStockBajoAsync()
        {
            var productos = await _context.Productos
                .Where(p => p.Activo && p.Stock <= p.StockMinimo)
                .OrderBy(p => p.Stock)
                .ToListAsync();

            if (!productos.Any()) return false;

            var destinatario = _config["Correo:Destinatario"];
            if (string.IsNullOrEmpty(destinatario)) return false;

            var asunto = $"⚠️ Inline Gym — {productos.Count} producto(s) con stock bajo ({DateTime.Now:dd/MM/yyyy})";
            var cuerpo = ConstruirCuerpoHtml(productos);

            return await EnviarAsync(destinatario, asunto, cuerpo);
        }

        private string ConstruirCuerpoHtml(List<Producto> productos)
        {
            var sb = new StringBuilder();
            sb.Append(@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;'>
              <div style='background:#E31E24;color:white;padding:20px 30px;border-radius:10px 10px 0 0;'>
                <h2 style='margin:0;'>⚠️ Alerta de Stock Bajo</h2>
                <p style='margin:5px 0 0;'>Inline Gym — " + DateTime.Now.ToString("dd/MM/yyyy hh:mm tt") + @"</p>
              </div>
              <div style='background:#fff5f5;padding:20px 30px;border:1px solid #fbd5d5;'>
                <p>Los siguientes productos tienen stock igual o por debajo del mínimo:</p>
                <table style='width:100%;border-collapse:collapse;margin-top:10px;'>
                  <thead>
                    <tr style='background:#1a1a1a;color:white;'>
                      <th style='padding:10px;text-align:left;'>Producto</th>
                      <th style='padding:10px;text-align:center;'>Stock Actual</th>
                      <th style='padding:10px;text-align:center;'>Stock Mínimo</th>
                      <th style='padding:10px;text-align:center;'>Estado</th>
                    </tr>
                  </thead>
                  <tbody>");

            foreach (var p in productos)
            {
                var sinStock = p.Stock <= 0;
                var color = sinStock ? "#fff0f0" : "#fffdf0";
                var badge = sinStock
                    ? "<span style='background:#E31E24;color:white;padding:2px 8px;border-radius:20px;font-size:12px;'>SIN STOCK</span>"
                    : "<span style='background:#f59e0b;color:white;padding:2px 8px;border-radius:20px;font-size:12px;'>BAJO</span>";

                sb.Append($@"
                    <tr style='background:{color};'>
                      <td style='padding:10px;border-bottom:1px solid #fbd5d5;font-weight:600;'>{p.Nombre}</td>
                      <td style='padding:10px;border-bottom:1px solid #fbd5d5;text-align:center;font-weight:700;color:{(sinStock ? "#E31E24" : "#f59e0b")};'>{p.Stock}</td>
                      <td style='padding:10px;border-bottom:1px solid #fbd5d5;text-align:center;color:#6b7280;'>{p.StockMinimo}</td>
                      <td style='padding:10px;border-bottom:1px solid #fbd5d5;text-align:center;'>{badge}</td>
                    </tr>");
            }

            sb.Append(@"
                  </tbody>
                </table>
              </div>
              <div style='background:#1a1a1a;color:#9ca3af;padding:15px 30px;border-radius:0 0 10px 10px;font-size:12px;text-align:center;'>
                Este correo fue generado automáticamente por el sistema GymPOS — Inline Gym
              </div>
            </div>");

            return sb.ToString();
        }

        private async Task<bool> EnviarAsync(string destinatario, string asunto, string cuerpo)
        {
            try
            {
                var host = _config["Correo:SmtpHost"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Correo:SmtpPort"] ?? "587");
                var remite = _config["Correo:Remitente"] ?? "";
                var password = _config["Correo:Password"] ?? "";

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(remite, password)
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(remite, "Inline Gym POS"),
                    Subject = asunto,
                    Body = cuerpo,
                    IsBodyHtml = true
                };
                mail.To.Add(destinatario);

                await client.SendMailAsync(mail);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Cooldown: máximo 1 correo por hora
        private static DateTime _ultimoEnvio = DateTime.MinValue;

        public async Task<bool> VerificarYEnviarStockBajoAsync(List<int> productosAfectadosIds)
        {

            if (_config["Correo:Habilitado"] != "true") return false;

            // Si ya se envió hace menos de 1 hora, no enviar
            if ((DateTime.Now - _ultimoEnvio).TotalHours < 1) return false;

            // Verificar si alguno de los productos afectados está en stock bajo
            var hayStockBajo = await _context.Productos
                .Where(p => productosAfectadosIds.Contains(p.Id) && p.Activo && p.Stock <= p.StockMinimo)
                .AnyAsync();

            if (!hayStockBajo) return false;

            var enviado = await EnviarAlertaStockBajoAsync();
            if (enviado) _ultimoEnvio = DateTime.Now;
            return enviado;
        }
    }
}