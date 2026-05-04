using ClosedXML.Excel;
using GymPOS.Data;
using GymPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace GymPOS.Services
{
    public class ReporteService
    {
        private readonly AppDbContext _context;
        private readonly CajaService _cajaService;

        public ReporteService(AppDbContext context, CajaService cajaService)
        {
            _context = context;
            _cajaService = cajaService;
        }

        // Reporte del turno activo actual
        public async Task<byte[]> GenerarReporteTurnoExcelAsync()
        {
            var turnoId = await _cajaService.GetTurnoActivoIdAsync();
            return await GenerarReportePorTurnoIdAsync(turnoId);
        }

        // Reporte por TurnoId específico (usado al cerrar caja)
        public async Task<byte[]> GenerarReportePorTurnoIdAsync(int turnoId)
        {
            var ventas = await _context.Ventas
                .Include(v => v.Detalles).ThenInclude(d => d.Producto)
                .Where(v => v.TurnoId == turnoId)
                .OrderBy(v => v.Fecha)
                .ToListAsync();

            var entradas = await _context.EntradasProducto
                .Include(e => e.Producto)
                .Where(e => e.TurnoId == turnoId)
                .OrderBy(e => e.Fecha)
                .ToListAsync();

            var productos = await _context.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.Marca)
                .ToListAsync();

            var conteos = await _context.ContesFisicos
                .Include(c => c.Producto)
                .Where(c => c.TurnoId == turnoId)
                .OrderBy(c => c.Fecha)
                .ToListAsync();

            var movimientos = await _context.MovimientosCaja
                .Where(m => m.TurnoId == turnoId)
                .OrderBy(m => m.Fecha)
                .ToListAsync();

            var fechaTurno = movimientos.FirstOrDefault()?.Fecha.Date ?? DateTime.Today;

            return ConstruirExcel(ventas, entradas, productos, conteos, movimientos, fechaTurno);
        }

        // Reporte por fecha (histórico)
        public async Task<byte[]> GenerarReportePorFechaAsync(DateTime fecha)
        {
            var hoy = fecha.Date;

            var ventas = await _context.Ventas
                .Include(v => v.Detalles).ThenInclude(d => d.Producto)
                .Where(v => v.Fecha.Date == hoy)
                .OrderBy(v => v.Fecha)
                .ToListAsync();

            var entradas = await _context.EntradasProducto
                .Include(e => e.Producto)
                .Where(e => e.Fecha.Date == hoy)
                .OrderBy(e => e.Fecha)
                .ToListAsync();

            var productos = await _context.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.Marca)
                .ToListAsync();

            var conteos = await _context.ContesFisicos
                .Include(c => c.Producto)
                .Where(c => c.Fecha.Date == hoy)
                .OrderBy(c => c.Fecha)
                .ToListAsync();

            var movimientos = await _context.MovimientosCaja
                .Where(m => m.Fecha.Date == hoy)
                .OrderBy(m => m.Fecha)
                .ToListAsync();

            return ConstruirExcel(ventas, entradas, productos, conteos, movimientos, hoy);
        }

        public async Task<List<DateTime>> GetFechasConRegistrosAsync()
        {
            return await _context.MovimientosCaja
                .Where(m => m.Tipo == "Apertura")
                .Select(m => m.Fecha.Date)
                .Distinct()
                .OrderByDescending(f => f)
                .ToListAsync();
        }

        // ===== CONSTRUCCIÓN DEL EXCEL =====
        private byte[] ConstruirExcel(
            List<Venta> ventas,
            List<EntradaProducto> entradas,
            List<Producto> productos,
            List<ConteoFisico> conteos,
            List<MovimientoCaja> movimientos,
            DateTime fecha)
        {
            var hoy = fecha.Date;
            var colorVerde = XLColor.FromHtml("#1B5E20");
            var colorGris = XLColor.FromHtml("#F5F5F5");
            var colorRojo = XLColor.FromHtml("#C62828");
            var colorAzul = XLColor.FromHtml("#0D47A1");

            using var wb = new XLWorkbook();

            // ===== HOJA 1: RESUMEN DEL DÍA =====
            var ws1 = wb.Worksheets.Add("Resumen del Día");

            ws1.Cell(1, 1).Value = "INLINE GYM - REPORTE DEL DÍA";
            ws1.Cell(1, 1).Style.Font.Bold = true;
            ws1.Cell(1, 1).Style.Font.FontSize = 16;
            ws1.Cell(1, 1).Style.Font.FontColor = colorVerde;
            ws1.Range("A1:F1").Merge();

            ws1.Cell(2, 1).Value = $"Fecha: {hoy:dddd dd 'de' MMMM 'de' yyyy}";
            ws1.Cell(2, 1).Style.Font.Italic = true;
            ws1.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy hh:mm tt}";
            ws1.Cell(3, 1).Style.Font.Italic = true;

            var apertura = movimientos.FirstOrDefault(m => m.Tipo == "Apertura");
            ws1.Cell(5, 1).Value = "APERTURA DE CAJA";
            ws1.Cell(5, 1).Style.Font.Bold = true;
            ws1.Cell(5, 1).Style.Fill.BackgroundColor = colorVerde;
            ws1.Cell(5, 1).Style.Font.FontColor = XLColor.White;
            ws1.Range("A5:F5").Merge();

            ws1.Cell(6, 1).Value = "Responsable";
            ws1.Cell(6, 2).Value = apertura?.Descripcion ?? "-";
            ws1.Cell(7, 1).Value = "Hora apertura";
            ws1.Cell(7, 2).Value = apertura?.Fecha.ToString("hh:mm tt") ?? "-";
            ws1.Cell(8, 1).Value = "Monto inicial";
            ws1.Cell(8, 2).Value = apertura?.Monto ?? 0;
            ws1.Cell(8, 2).Style.NumberFormat.Format = "₡#,##0";

            var totalEfectivo = ventas.Where(v => v.MetodoPago == "Efectivo").Sum(v => v.Total);
            var totalSinpe = ventas.Where(v => v.MetodoPago == "SINPE Móvil").Sum(v => v.Total);
            var totalTarjeta = ventas.Where(v => v.MetodoPago == "Tarjeta").Sum(v => v.Total);
            var totalVentas = ventas.Sum(v => v.Total);
            var totalEgresos = Math.Abs(movimientos.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto));

            ws1.Cell(10, 1).Value = "RESUMEN DE VENTAS";
            ws1.Cell(10, 1).Style.Font.Bold = true;
            ws1.Cell(10, 1).Style.Fill.BackgroundColor = colorVerde;
            ws1.Cell(10, 1).Style.Font.FontColor = XLColor.White;
            ws1.Range("A10:F10").Merge();

            ws1.Cell(11, 1).Value = "Ventas en Efectivo";
            ws1.Cell(11, 2).Value = totalEfectivo;
            ws1.Cell(11, 2).Style.NumberFormat.Format = "₡#,##0";

            ws1.Cell(12, 1).Value = "Ventas SINPE Móvil";
            ws1.Cell(12, 2).Value = totalSinpe;
            ws1.Cell(12, 2).Style.NumberFormat.Format = "₡#,##0";

            ws1.Cell(13, 1).Value = "Ventas Tarjeta";
            ws1.Cell(13, 2).Value = totalTarjeta;
            ws1.Cell(13, 2).Style.NumberFormat.Format = "₡#,##0";

            ws1.Cell(14, 1).Value = "Total Ventas";
            ws1.Cell(14, 1).Style.Font.Bold = true;
            ws1.Cell(14, 2).Value = totalVentas;
            ws1.Cell(14, 2).Style.NumberFormat.Format = "₡#,##0";
            ws1.Cell(14, 2).Style.Font.Bold = true;

            ws1.Cell(15, 1).Value = "Total Egresos";
            ws1.Cell(15, 2).Value = -totalEgresos;
            ws1.Cell(15, 2).Style.NumberFormat.Format = "₡#,##0";
            ws1.Cell(15, 2).Style.Font.FontColor = colorRojo;

            ws1.Cell(16, 1).Value = "Efectivo Esperado en Caja";
            ws1.Cell(16, 1).Style.Font.Bold = true;
            ws1.Cell(16, 2).Value = (apertura?.Monto ?? 0) + totalEfectivo - totalEgresos;
            ws1.Cell(16, 2).Style.NumberFormat.Format = "₡#,##0";
            ws1.Cell(16, 2).Style.Font.Bold = true;

            var cortes = movimientos.Where(m => m.Tipo == "Corte").ToList();
            if (cortes.Any())
            {
                ws1.Cell(18, 1).Value = "CORTES DEL DÍA";
                ws1.Cell(18, 1).Style.Font.Bold = true;
                ws1.Cell(18, 1).Style.Fill.BackgroundColor = colorAzul;
                ws1.Cell(18, 1).Style.Font.FontColor = XLColor.White;
                ws1.Range("A18:F18").Merge();

                int rowCorte = 19;
                ws1.Cell(rowCorte, 1).Value = "Hora";
                ws1.Cell(rowCorte, 2).Value = "Descripción";
                ws1.Cell(rowCorte, 3).Value = "Monto Contado";
                ws1.Row(rowCorte).Style.Font.Bold = true;
                rowCorte++;

                foreach (var corte in cortes)
                {
                    ws1.Cell(rowCorte, 1).Value = corte.Fecha.ToString("hh:mm tt");
                    ws1.Cell(rowCorte, 2).Value = corte.Descripcion;
                    ws1.Cell(rowCorte, 3).Value = corte.Monto;
                    ws1.Cell(rowCorte, 3).Style.NumberFormat.Format = "₡#,##0";
                    rowCorte++;
                }
            }

            var cierre = movimientos.LastOrDefault(m => m.Tipo == "CierreDefinitivo");
            if (cierre != null)
            {
                int rowCierre = cortes.Any() ? 22 + cortes.Count : 18;
                ws1.Cell(rowCierre, 1).Value = "CIERRE DEFINITIVO";
                ws1.Cell(rowCierre, 1).Style.Font.Bold = true;
                ws1.Cell(rowCierre, 1).Style.Fill.BackgroundColor = colorRojo;
                ws1.Cell(rowCierre, 1).Style.Font.FontColor = XLColor.White;
                ws1.Range(rowCierre, 1, rowCierre, 6).Merge();
                rowCierre++;
                ws1.Cell(rowCierre, 1).Value = "Hora cierre";
                ws1.Cell(rowCierre, 2).Value = cierre.Fecha.ToString("hh:mm tt");
                rowCierre++;
                ws1.Cell(rowCierre, 1).Value = "Monto físico contado";
                ws1.Cell(rowCierre, 2).Value = cierre.Monto;
                ws1.Cell(rowCierre, 2).Style.NumberFormat.Format = "₡#,##0";
                rowCierre++;
                ws1.Cell(rowCierre, 1).Value = "Observación";
                ws1.Cell(rowCierre, 2).Value = cierre.Descripcion;
            }

            ws1.Columns().AdjustToContents();

            // ===== HOJA 2: INVENTARIO POR TURNO =====
            var ws2 = wb.Worksheets.Add("Inventario por Turno");
            ws2.Cell(1, 1).Value = "INVENTARIO POR TURNO";
            ws2.Cell(1, 1).Style.Font.Bold = true;
            ws2.Cell(1, 1).Style.Font.FontSize = 14;
            ws2.Cell(1, 1).Style.Font.FontColor = colorVerde;
            ws2.Range("A1:G1").Merge();

            var ordenTurnos = new List<string> { "Mañana", "Mediodía", "Tarde", "Noche" };
            var turnosConteo = conteos.Select(c => c.Turno).Distinct()
                .OrderBy(t => ordenTurnos.IndexOf(t)).ToList();

            int rowInv = 3;
            foreach (var turno in turnosConteo)
            {
                var conteosDelTurno = conteos.Where(c => c.Turno == turno).ToList();
                var horaConteo = conteosDelTurno.FirstOrDefault()?.Fecha.ToString("hh:mm tt") ?? "";
                var esTurnoConVentas = turno != "Mañana";

                ws2.Cell(rowInv, 1).Value = $"📋 CONTEO {turno.ToUpper()} — {horaConteo}";
                ws2.Cell(rowInv, 1).Style.Font.Bold = true;
                ws2.Cell(rowInv, 1).Style.Font.FontSize = 12;
                ws2.Cell(rowInv, 1).Style.Fill.BackgroundColor =
                    turno == "Mañana" ? XLColor.FromHtml("#E3F2FD") :
                    turno == "Mediodía" ? XLColor.FromHtml("#FFF9C4") :
                    turno == "Tarde" ? XLColor.FromHtml("#FFE0B2") :
                    XLColor.FromHtml("#FFEBEE");
                ws2.Range(rowInv, 1, rowInv, esTurnoConVentas ? 6 : 5).Merge();
                rowInv++;

                ws2.Cell(rowInv, 1).Value = "Producto";
                ws2.Cell(rowInv, 2).Value = "Precio";
                ws2.Cell(rowInv, 3).Value = "Stock Sistema";
                ws2.Cell(rowInv, 4).Value = "Conteo Físico";
                ws2.Cell(rowInv, 5).Value = "Diferencia";
                if (esTurnoConVentas) ws2.Cell(rowInv, 6).Value = "Total ₡ Vendido";

                var headerRange = ws2.Range(rowInv, 1, rowInv, esTurnoConVentas ? 6 : 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = colorVerde;
                headerRange.Style.Font.FontColor = XLColor.White;
                rowInv++;

                decimal totalTurno = 0;
                foreach (var c in conteosDelTurno.GroupBy(c => c.ProductoId).Select(g => g.Last()))
                {
                    var diff = c.CantidadContada - c.CantidadSistema;
                    var totalVendidoProducto = esTurnoConVentas ? Math.Abs(diff < 0 ? diff : 0) * (c.Producto?.Precio ?? 0) : 0;
                    totalTurno += totalVendidoProducto;

                    ws2.Cell(rowInv, 1).Value = c.Producto?.Nombre;
                    ws2.Cell(rowInv, 2).Value = c.Producto?.Precio;
                    ws2.Cell(rowInv, 2).Style.NumberFormat.Format = "₡#,##0";
                    ws2.Cell(rowInv, 3).Value = c.CantidadSistema;
                    ws2.Cell(rowInv, 4).Value = c.CantidadContada;
                    ws2.Cell(rowInv, 5).Value = diff;
                    ws2.Cell(rowInv, 5).Style.Font.FontColor = diff == 0 ? XLColor.Green : diff < 0 ? XLColor.Red : XLColor.Orange;
                    if (esTurnoConVentas)
                    {
                        ws2.Cell(rowInv, 6).Value = totalVendidoProducto;
                        ws2.Cell(rowInv, 6).Style.NumberFormat.Format = "₡#,##0";
                    }
                    if (rowInv % 2 == 0)
                        ws2.Range(rowInv, 1, rowInv, esTurnoConVentas ? 6 : 5).Style.Fill.BackgroundColor = colorGris;
                    rowInv++;
                }

                if (esTurnoConVentas)
                {
                    ws2.Cell(rowInv, 5).Value = "TOTAL VENDIDO";
                    ws2.Cell(rowInv, 5).Style.Font.Bold = true;
                    ws2.Cell(rowInv, 6).Value = totalTurno;
                    ws2.Cell(rowInv, 6).Style.NumberFormat.Format = "₡#,##0";
                    ws2.Cell(rowInv, 6).Style.Font.Bold = true;
                    ws2.Cell(rowInv, 6).Style.Font.FontColor = colorVerde;
                }
                rowInv += 2;
            }
            ws2.Columns().AdjustToContents();

            // ===== HOJA 3: DETALLE DE VENTAS =====
            var ws3 = wb.Worksheets.Add("Detalle de Ventas");
            ws3.Cell(1, 1).Value = "DETALLE DE VENTAS DEL DÍA";
            ws3.Cell(1, 1).Style.Font.Bold = true;
            ws3.Cell(1, 1).Style.Font.FontSize = 14;
            ws3.Cell(1, 1).Style.Font.FontColor = colorVerde;
            ws3.Range("A1:F1").Merge();

            ws3.Cell(3, 1).Value = "Hora";
            ws3.Cell(3, 2).Value = "Producto";
            ws3.Cell(3, 3).Value = "Cantidad";
            ws3.Cell(3, 4).Value = "Precio Unit.";
            ws3.Cell(3, 5).Value = "Subtotal";
            ws3.Cell(3, 6).Value = "Método Pago";
            ws3.Row(3).Style.Font.Bold = true;
            ws3.Row(3).Style.Fill.BackgroundColor = colorVerde;
            ws3.Row(3).Style.Font.FontColor = XLColor.White;

            int row3 = 4;
            foreach (var venta in ventas)
            {
                foreach (var detalle in venta.Detalles)
                {
                    ws3.Cell(row3, 1).Value = venta.Fecha.ToString("hh:mm tt");
                    ws3.Cell(row3, 2).Value = detalle.Producto?.Nombre;
                    ws3.Cell(row3, 3).Value = detalle.Cantidad;
                    ws3.Cell(row3, 4).Value = detalle.PrecioUnitario;
                    ws3.Cell(row3, 4).Style.NumberFormat.Format = "₡#,##0";
                    ws3.Cell(row3, 5).Value = detalle.Subtotal;
                    ws3.Cell(row3, 5).Style.NumberFormat.Format = "₡#,##0";
                    ws3.Cell(row3, 6).Value = venta.MetodoPago;
                    if (row3 % 2 == 0)
                        ws3.Range(row3, 1, row3, 6).Style.Fill.BackgroundColor = colorGris;
                    row3++;
                }
            }

            row3++;
            ws3.Cell(row3, 4).Value = "TOTAL";
            ws3.Cell(row3, 4).Style.Font.Bold = true;
            ws3.Cell(row3, 5).Value = ventas.Sum(v => v.Total);
            ws3.Cell(row3, 5).Style.NumberFormat.Format = "₡#,##0";
            ws3.Cell(row3, 5).Style.Font.Bold = true;
            ws3.Cell(row3, 5).Style.Font.FontColor = colorVerde;
            ws3.Columns().AdjustToContents();

            // ===== HOJA 4: ENTRADAS =====
            var ws4 = wb.Worksheets.Add("Entradas de Producto");
            ws4.Cell(1, 1).Value = "ENTRADAS DE PRODUCTO DEL DÍA";
            ws4.Cell(1, 1).Style.Font.Bold = true;
            ws4.Cell(1, 1).Style.Font.FontSize = 14;
            ws4.Cell(1, 1).Style.Font.FontColor = colorVerde;
            ws4.Range("A1:E1").Merge();

            ws4.Cell(3, 1).Value = "Hora";
            ws4.Cell(3, 2).Value = "Producto";
            ws4.Cell(3, 3).Value = "Cantidad";
            ws4.Cell(3, 4).Value = "Proveedor";
            ws4.Cell(3, 5).Value = "Costo Factura";
            ws4.Row(3).Style.Font.Bold = true;
            ws4.Row(3).Style.Fill.BackgroundColor = colorVerde;
            ws4.Row(3).Style.Font.FontColor = XLColor.White;

            int row4 = 4;
            foreach (var entrada in entradas)
            {
                ws4.Cell(row4, 1).Value = entrada.Fecha.ToString("hh:mm tt");
                ws4.Cell(row4, 2).Value = entrada.Producto?.Nombre;
                ws4.Cell(row4, 3).Value = entrada.Cantidad;
                ws4.Cell(row4, 4).Value = entrada.Proveedor ?? "-";
                ws4.Cell(row4, 5).Value = entrada.CostoFactura;
                ws4.Cell(row4, 5).Style.NumberFormat.Format = "₡#,##0";
                if (row4 % 2 == 0)
                    ws4.Range(row4, 1, row4, 5).Style.Fill.BackgroundColor = colorGris;
                row4++;
            }

            if (entradas.Any())
            {
                row4++;
                ws4.Cell(row4, 4).Value = "TOTAL FACTURAS";
                ws4.Cell(row4, 4).Style.Font.Bold = true;
                ws4.Cell(row4, 5).Value = entradas.Sum(e => e.CostoFactura);
                ws4.Cell(row4, 5).Style.NumberFormat.Format = "₡#,##0";
                ws4.Cell(row4, 5).Style.Font.Bold = true;
                ws4.Cell(row4, 5).Style.Font.FontColor = colorRojo;
            }
            ws4.Columns().AdjustToContents();

            // ===== HOJA 5: EGRESOS =====
            var ws5 = wb.Worksheets.Add("Egresos");
            ws5.Cell(1, 1).Value = "EGRESOS DEL DÍA";
            ws5.Cell(1, 1).Style.Font.Bold = true;
            ws5.Cell(1, 1).Style.Font.FontSize = 14;
            ws5.Cell(1, 1).Style.Font.FontColor = colorRojo;
            ws5.Range("A1:D1").Merge();

            ws5.Cell(3, 1).Value = "Hora";
            ws5.Cell(3, 2).Value = "Descripción";
            ws5.Cell(3, 3).Value = "Método";
            ws5.Cell(3, 4).Value = "Monto";
            ws5.Row(3).Style.Font.Bold = true;
            ws5.Row(3).Style.Fill.BackgroundColor = colorRojo;
            ws5.Row(3).Style.Font.FontColor = XLColor.White;

            int row5 = 4;
            var egresos = movimientos.Where(m => m.Tipo == "Egreso").ToList();
            foreach (var egreso in egresos)
            {
                ws5.Cell(row5, 1).Value = egreso.Fecha.ToString("hh:mm tt");
                ws5.Cell(row5, 2).Value = egreso.Descripcion;
                ws5.Cell(row5, 3).Value = egreso.MetodoPago;
                ws5.Cell(row5, 4).Value = Math.Abs(egreso.Monto);
                ws5.Cell(row5, 4).Style.NumberFormat.Format = "₡#,##0";
                ws5.Cell(row5, 4).Style.Font.FontColor = colorRojo;
                if (row5 % 2 == 0)
                    ws5.Range(row5, 1, row5, 4).Style.Fill.BackgroundColor = colorGris;
                row5++;
            }

            if (egresos.Any())
            {
                row5++;
                ws5.Cell(row5, 3).Value = "TOTAL EGRESOS";
                ws5.Cell(row5, 3).Style.Font.Bold = true;
                ws5.Cell(row5, 4).Value = totalEgresos;
                ws5.Cell(row5, 4).Style.NumberFormat.Format = "₡#,##0";
                ws5.Cell(row5, 4).Style.Font.Bold = true;
                ws5.Cell(row5, 4).Style.Font.FontColor = colorRojo;
            }
            ws5.Columns().AdjustToContents();

            // ===== HOJA 6: MOVIMIENTOS =====
            var ws6 = wb.Worksheets.Add("Movimientos de Caja");
            ws6.Cell(1, 1).Value = "MOVIMIENTOS DE CAJA DEL DÍA";
            ws6.Cell(1, 1).Style.Font.Bold = true;
            ws6.Cell(1, 1).Style.Font.FontSize = 14;
            ws6.Cell(1, 1).Style.Font.FontColor = colorAzul;
            ws6.Range("A1:E1").Merge();

            ws6.Cell(3, 1).Value = "Hora";
            ws6.Cell(3, 2).Value = "Tipo";
            ws6.Cell(3, 3).Value = "Descripción";
            ws6.Cell(3, 4).Value = "Método";
            ws6.Cell(3, 5).Value = "Monto";
            ws6.Row(3).Style.Font.Bold = true;
            ws6.Row(3).Style.Fill.BackgroundColor = colorAzul;
            ws6.Row(3).Style.Font.FontColor = XLColor.White;

            int row6 = 4;
            foreach (var mov in movimientos)
            {
                ws6.Cell(row6, 1).Value = mov.Fecha.ToString("hh:mm tt");
                ws6.Cell(row6, 2).Value = mov.Tipo;
                ws6.Cell(row6, 3).Value = mov.Descripcion;
                ws6.Cell(row6, 4).Value = mov.MetodoPago;
                ws6.Cell(row6, 5).Value = mov.Monto;
                ws6.Cell(row6, 5).Style.NumberFormat.Format = "₡#,##0";
                ws6.Cell(row6, 5).Style.Font.FontColor = mov.Monto >= 0 ? XLColor.Green : XLColor.Red;
                if (row6 % 2 == 0)
                    ws6.Range(row6, 1, row6, 5).Style.Fill.BackgroundColor = colorGris;
                row6++;
            }
            ws6.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}