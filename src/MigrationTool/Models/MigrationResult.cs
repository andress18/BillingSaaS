namespace BillingSaaS.MigrationTool.Models;

public class MigrationResult
{
    public int TotalClientesDetectados { get; set; }
    public int ClientesProcesadosExitosamente { get; set; }
    public int ClientesOmitidos { get; set; }
    public int ClientesConError { get; set; }
    public int TotalFacturasMigradas { get; set; }
    public int TotalClientesCatalogoCreados { get; set; }
    public int TotalProductosCatalogoCreados { get; set; }
    public int FirmasValidas { get; set; }
    public int FirmasCaducadas { get; set; }
    public int FirmasSinCertificado { get; set; }

    public List<MigrationErrorDetail> Errores { get; set; } = new();
    public List<string> OmitidosDetalle { get; set; } = new();
}

public class MigrationErrorDetail
{
    public string IdentificadorCliente { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}

public class MigrationOptions
{
    public Guid PartnerId { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222222"); // Partner Oficial por defecto
    public string PlanCodigoDefault { get; set; } = "MIGRACION_SISTEMA";
    public string SourceType { get; set; } = "Database"; // "Database" o "Json"
    public string? JsonFilePath { get; set; }
    public string? LegacyConnectionString { get; set; }
    public string? TargetConnectionString { get; set; }
    public string? FilterCsvPath { get; set; } = "clientes_partner.csv";
    public bool DryRun { get; set; } = false;
    public bool OmitirFacturasConError { get; set; } = true;
    public int LimiteClientes { get; set; } = 0; // 0 = sin límite
}
