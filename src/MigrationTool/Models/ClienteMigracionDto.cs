namespace BillingSaaS.MigrationTool.Models;

public class ClienteMigracionDto
{
    // ==========================================
    // 1. CUENTA Y TENANT (Identity / Partner)
    // ==========================================
    public Guid PartnerId { get; set; }           // El GUID del partner actual
    public string NombreOrganizacion { get; set; } = string.Empty; // Razón Social o Nombre del negocio
    public string Username { get; set; } = string.Empty;          // Usuario antiguo
    public string Email { get; set; } = string.Empty;             // Correo electrónico
    public string PasswordPlana { get; set; } = string.Empty;      // Password actual del usuario

    // ==========================================
    // 2. SUSCRIPCIÓN Y PLAN
    // ==========================================
    public string PlanCodigo { get; set; } = "MIGRACION_SISTEMA"; // O "MIGRACION_FIRMA"
    public DateTime FechaInicioPlan { get; set; } = DateTime.UtcNow;
    public DateTime FechaFinPlan { get; set; }    // Fecha fin actual de su plan
    public string Frecuencia { get; set; } = "ANUAL"; // "ANUAL" o "MENSUAL"
    public int DiasGracia { get; set; } = 3;

    // ==========================================
    // 3. CERTIFICADO DIGITAL Y FIRMA (.p12)
    // ==========================================
    public string CertificadoBase64 { get; set; } = string.Empty; // Firma .p12 en Base64
    public string PasswordCertificado { get; set; } = string.Empty; // Clave plana de la firma

    // ==========================================
    // 4. DATOS TRIBUTARIOS DEL EMISOR
    // (Pueden extraerse automáticamente del XML de su última factura)
    // ==========================================
    public string Ruc { get; set; } = string.Empty;               // 13 dígitos numéricos
    public string RazonSocial { get; set; } = string.Empty;       // Razón social registrada en SRI
    public string? NombreComercial { get; set; }  // Nombre comercial
    public string DireccionMatriz { get; set; } = string.Empty;   // Dirección matriz
    public string? DireccionEstablecimiento { get; set; }
    public string CodigoEstablecimiento { get; set; } = "001"; // Ej: "001"
    public string PuntoEmision { get; set; } = "001";          // Ej: "001"
    public int Ambiente { get; set; } = 2;                     // 1: Pruebas, 2: Producción
    public bool ObligadoContabilidad { get; set; } = false;
    public string? RegimenRimpe { get; set; }                  // "CONTRIBUYENTE RÉGIMEN RIMPE", etc.
    public string? ContribuyenteEspecial { get; set; }
    public int UltimoSecuencialFactura { get; set; }           // Max correlativo emitido en el sistema anterior
    public string? Logo { get; set; }                          // Logotipo del negocio (Base64 / Data URI)

    // ==========================================
    // 5. HISTORIAL DE FACTURAS EMITIDAS
    // ==========================================
    public List<FacturaMigracionDto> Facturas { get; set; } = new();

    // ==========================================
    // 6. CATÁLOGOS BASE (Compradores y Productos del sistema anterior)
    // ==========================================
    public List<CompradorMigracionDto> Compradores { get; set; } = new();
    public List<DetalleFacturaMigracionDto> Productos { get; set; } = new();
}

public class FacturaMigracionDto
{
    public string ClaveAcceso { get; set; } = string.Empty;       // 49 dígitos
    public string Secuencial { get; set; } = string.Empty;        // 9 dígitos (ej: 000000123)
    public string Establecimiento { get; set; } = "001";   // "001"
    public string PuntoEmision { get; set; } = "001";      // "001"
    public DateTime FechaEmision { get; set; }
    public string Estado { get; set; } = "AUTORIZADO";            // "AUTORIZADO", "DEVUELTA", etc.
    public string? NumeroAutorizacion { get; set; }
    public DateTime? FechaAutorizacion { get; set; }
    public string? MensajeErrorSri { get; set; }
    
    // Contenido XML
    public string XmlOriginal { get; set; } = string.Empty;       // XML sin firma o estructura
    public string? XmlFirmado { get; set; }       // XML con firma XAdES-BES
    
    // Totales y Adquirente (se extraen directamente del XML)
    public decimal TotalSinImpuestos { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal ImporteTotal { get; set; }
    public string FormaPago { get; set; } = "01";
    
    public CompradorMigracionDto Comprador { get; set; } = new();
    public List<DetalleFacturaMigracionDto> Detalles { get; set; } = new();
}

public class CompradorMigracionDto
{
    public string TipoIdentificacion { get; set; } = "07"; // "04" (RUC), "05" (Cédula), "07" (Consumidor Final)
    public string Identificacion { get; set; } = "9999999999999";
    public string RazonSocial { get; set; } = "CONSUMIDOR FINAL";
    public string? Direccion { get; set; }
    public string? CorreoElectronico { get; set; }
}

public class DetalleFacturaMigracionDto
{
    public string CodigoPrincipal { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public List<ImpuestoMigracionDto> Impuestos { get; set; } = new();
}

public class ImpuestoMigracionDto
{
    public string Codigo { get; set; } = "2";           // "2" (IVA)
    public string CodigoPorcentaje { get; set; } = "2"; // "0", "2" (12%), "4" (15%), etc.
    public decimal Tarifa { get; set; } = 15.00m;       // 15.00
    public decimal BaseImponible { get; set; }
    public decimal Valor { get; set; }
}
