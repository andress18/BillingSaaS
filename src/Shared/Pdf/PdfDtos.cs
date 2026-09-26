namespace BillingSaaS.Shared.Pdf;

public record EmisorPdfDto
{
    public string? RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public string? Ruc { get; init; }
    public string? DireccionMatriz { get; init; }
    public string? DireccionEstablecimiento { get; init; }
    public string? ContribuyenteEspecial { get; init; }
    public string? ObligadoContabilidad { get; init; }
    public string? RegimenRimpe { get; init; }
}

public record CompradorPdfDto
{
    public string? RazonSocial { get; init; }
    public string? Identificacion { get; init; }
    public string? Direccion { get; init; }
    public string? CorreoElectronico { get; init; }
}

public record ImpuestoPdfDto
{
    public string Codigo { get; init; } = "2";
    public string CodigoPorcentaje { get; init; } = "4";
    public decimal Tarifa { get; init; }
    public decimal BaseImponible { get; init; }
    public decimal Valor { get; init; }
}

public record DetallePdfDto
{
    public string CodigoPrincipal { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Descuento { get; init; }
    public decimal PrecioTotalSinImpuesto { get; init; }
    public List<ImpuestoPdfDto> Impuestos { get; init; } = [];
}

public record CampoAdicionalPdfDto(string Nombre, string Valor);

public record FacturaPdfRequestDto
{
    public string Establecimiento { get; init; } = "001";
    public string PuntoEmision { get; init; } = "001";
    public string Secuencial { get; init; } = "000000001";
    public string ClaveAcceso { get; init; } = string.Empty;
    public string? NumeroAutorizacion { get; init; }
    public DateTime? FechaAutorizacion { get; init; }
    public int Ambiente { get; init; } = 1;
    public DateTime FechaEmision { get; init; } = DateTime.Today;
    public string? RazonSocialEmisor { get; init; }
    public string? RucEmisor { get; init; }
    public string? DireccionMatrizEmisor { get; init; }
    public string? RegimenRimpeEmisor { get; init; }
    public decimal TotalSinImpuestos { get; init; }
    public decimal TotalDescuento { get; init; }
    public decimal ImporteTotal { get; init; }
    public CompradorPdfDto? Cliente { get; init; }
    public EmisorPdfDto? Emisor { get; init; }
    public List<DetallePdfDto> Detalles { get; init; } = [];
    public List<CampoAdicionalPdfDto> CamposAdicionales { get; init; } = [];
    public string FormaPago { get; init; } = "01";
    public decimal? Plazo { get; init; }
    public string? UnidadTiempo { get; init; }
    public string? LogoBase64 { get; init; }
    public string? RucProveedor { get; init; }
    public string? NombreProveedor { get; init; }
}

public record NotaCreditoPdfRequestDto
{
    public string Establecimiento { get; init; } = "001";
    public string PuntoEmision { get; init; } = "001";
    public string Secuencial { get; init; } = "000000001";
    public string ClaveAcceso { get; init; } = string.Empty;
    public string? NumeroAutorizacion { get; init; }
    public DateTime? FechaAutorizacion { get; init; }
    public int Ambiente { get; init; } = 1;
    public DateTime FechaEmision { get; init; } = DateTime.Today;
    public string? CodDocModificado { get; init; }
    public string? NumDocModificado { get; init; }
    public DateTime FechaEmisionDocSustento { get; init; } = DateTime.Today;
    public string? Motivo { get; init; }
    public decimal TotalSinImpuestos { get; init; }
    public decimal TotalDescuento { get; init; }
    public decimal ValorModificacion { get; init; }
    public CompradorPdfDto? Cliente { get; init; }
    public EmisorPdfDto? Emisor { get; init; }
    public List<DetallePdfDto> Detalles { get; init; } = [];
    public List<CampoAdicionalPdfDto> CamposAdicionales { get; init; } = [];
    public string? LogoBase64 { get; init; }
    public string? RucProveedor { get; init; }
    public string? NombreProveedor { get; init; }
}

public record MotivoDebitoPdfDto(string Razon, decimal Valor);
public record PagoDebitoPdfDto(string FormaPago, decimal Total, decimal? Plazo, string? UnidadTiempo);

public record NotaDebitoPdfRequestDto
{
    public string Establecimiento { get; init; } = "001";
    public string PuntoEmision { get; init; } = "001";
    public string Secuencial { get; init; } = "000000001";
    public string ClaveAcceso { get; init; } = string.Empty;
    public string? NumeroAutorizacion { get; init; }
    public DateTime? FechaAutorizacion { get; init; }
    public int Ambiente { get; init; } = 1;
    public DateTime FechaEmision { get; init; } = DateTime.Today;
    public string? CodDocModificado { get; init; }
    public string? NumDocModificado { get; init; }
    public DateTime FechaEmisionDocSustento { get; init; } = DateTime.Today;
    public decimal TotalSinImpuestos { get; init; }
    public decimal ValorTotal { get; init; }
    public CompradorPdfDto? Cliente { get; init; }
    public EmisorPdfDto? Emisor { get; init; }
    public List<MotivoDebitoPdfDto> Motivos { get; init; } = [];
    public List<ImpuestoPdfDto> Impuestos { get; init; } = [];
    public List<PagoDebitoPdfDto> Pagos { get; init; } = [];
    public List<CampoAdicionalPdfDto> CamposAdicionales { get; init; } = [];
    public string? LogoBase64 { get; init; }
    public string? RucProveedor { get; init; }
    public string? NombreProveedor { get; init; }
}
