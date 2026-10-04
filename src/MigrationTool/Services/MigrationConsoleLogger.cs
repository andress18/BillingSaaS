using BillingSaaS.MigrationTool.Models;

namespace BillingSaaS.MigrationTool.Services;

public static class MigrationConsoleLogger
{
    public static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔════════════════════════════════════════════════════════════════════════════════╗
║                                                                                ║
║                  BILLINGSAAS - HERRAMIENTA DE MIGRACIÓN                        ║
║                 Migración Multi-Tenant & Facturación Electrónica               ║
║                                                                                ║
╚════════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintSummaryReport(MigrationResult result, TimeSpan duration)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("                      REPORTE RESUMEN DE LA MIGRACIÓN                          ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.WriteLine($"Duración del proceso:                 {duration.TotalSeconds:F2} segundos");
        Console.WriteLine($"Total Clientes Detectados:            {result.TotalClientesDetectados}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Clientes Migrados con Éxito:          {result.ClientesProcesadosExitosamente}");
        Console.ResetColor();

        if (result.ClientesOmitidos > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Clientes Omitidos (Ya Existentes):    {result.ClientesOmitidos}");
            Console.ResetColor();
        }

        if (result.ClientesConError > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Clientes con Error (Rollback):        {result.ClientesConError}");
            Console.ResetColor();
        }

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"Total Facturas Históricas Migradas:   {result.TotalFacturasMigradas}");
        Console.WriteLine($"Clientes de Catálogo Registrados:     {result.TotalClientesCatalogoCreados}");
        Console.ResetColor();

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine("ESTADO DE FIRMAS ELECTRÓNICAS (.p12):");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  * Firmas Válidas y Cifradas (AES-GCM): {result.FirmasValidas}");
        Console.ResetColor();

        if (result.FirmasCaducadas > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  * Firmas Caducadas (Cifradas):         {result.FirmasCaducadas}");
            Console.ResetColor();
        }

        if (result.FirmasSinCertificado > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  * Sin Firma o Archivo Inválido:        {result.FirmasSinCertificado}");
            Console.ResetColor();
        }

        if (result.OmitidosDetalle.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("DETALLE DE CLIENTES OMITIDOS:");
            Console.ResetColor();
            foreach (var om in result.OmitidosDetalle)
            {
                Console.WriteLine($"  - {om}");
            }
        }

        if (result.Errores.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("DETALLE DE ERRORES REGISTRADOS:");
            Console.ResetColor();
            foreach (var err in result.Errores)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [X] Cliente: {err.IdentificadorCliente} (RUC: {err.Ruc})");
                Console.ResetColor();
                Console.WriteLine($"      Error: {err.Mensaje}");
                if (!string.IsNullOrWhiteSpace(err.StackTrace))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    var firstLine = err.StackTrace.Split(Environment.NewLine).FirstOrDefault();
                    Console.WriteLine($"      Ubicación: {firstLine?.Trim()}");
                    Console.ResetColor();
                }
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine();
    }
}
