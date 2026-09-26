namespace BillingSaaS.Domain.Entities;

using System;
using System.Collections.Generic;
using System.Linq;

public class Factura : BaseAuditableEntity
{
    // 1. Aislamiento Multi-tenant (SaaS)
    public Guid TenantId { get; private set; }

    // Relación con el Emisor
    public int EmisorId { get; private set; }

    // Estado del comprobante en el flujo SRI (CREADA, RECIBIDA, AUTORIZADA, DEVUELTA, NO_AUTORIZADA)
    public string Estado { get; private set; } = "CREADA";
    public string? NumeroAutorizacion { get; private set; }
    public DateTime? FechaAutorizacion { get; private set; }
    public string? MensajeErrorSri { get; private set; }
    public string? XmlFirmado { get; private set; }

    // ==========================================
    // 1. INFORMACIÓN TRIBUTARIA (Cabecera)
    // ==========================================

    /// <summary>
    /// 1: Pruebas, 2: Producción
    /// </summary>
    public int Ambiente { get; private set; }

    /// <summary>
    /// 1: Emisión Normal
    /// </summary>
    public int TipoEmision { get; private set; }

    /// <summary>
    /// Máximo 300 caracteres
    /// </summary>
    public string RazonSocial { get; private set; } = null!;

    /// <summary>
    /// 13 dígitos numéricos
    /// </summary>
    public string Ruc { get; private set; } = null!;

    /// <summary>
    /// 49 dígitos numéricos
    /// </summary>
    public string ClaveAcceso { get; private set; } = null!;

    /// <summary>
    /// 01 para Factura
    /// </summary>
    public string CodDoc { get; private set; } = null!;

    /// <summary>
    /// 3 dígitos
    /// </summary>
    public string Establecimiento { get; private set; } = null!;

    /// <summary>
    /// 3 dígitos
    /// </summary>
    public string PuntoEmision { get; private set; } = null!;

    /// <summary>
    /// 9 dígitos
    /// </summary>
    public string Secuencial { get; private set; } = null!;

    /// <summary>
    /// Máximo 300 caracteres
    /// </summary>
    public string DireccionMatriz { get; private set; } = null!;

    /// <summary>
    /// Leyenda oficial SRI para contribuyentes RIMPE ("CONTRIBUYENTE RÉGIMEN RIMPE" o "CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE")
    /// </summary>
    public string? ContribuyenteRimpe { get; private set; }

    // ==========================================
    // 2. INFORMACIÓN DE LA FACTURA (Comprador y Totales)
    // ==========================================

    /// <summary>
    /// Formato: dd/mm/aaaa
    /// </summary>
    public DateTime FechaEmision { get; private set; }

    /// <summary>
    /// 04: RUC, 05: Cédula, 06: Pasaporte, 07: Consumidor Final, 08: Exterior
    /// </summary>
    public string TipoIdentificacionComprador { get; private set; } = null!;

    /// <summary>
    /// Máximo 300 caracteres
    /// </summary>
    public string RazonSocialComprador { get; private set; } = null!;

    /// <summary>
    /// Máximo 20 caracteres (Para consumidor final: 9999999999999)
    /// </summary>
    public string IdentificacionComprador { get; private set; } = null!;

    public decimal TotalSinImpuestos { get; private set; }
    public decimal TotalDescuento { get; private set; }
    public decimal ImporteTotal { get; private set; }

    // ==========================================
    // 2.1 FORMA DE PAGO SRI (Tabla 24)
    // ==========================================
    public string FormaPago { get; private set; } = "01"; // 01: Sin utilización del sistema financiero
    public decimal? Plazo { get; private set; }
    public string? UnidadTiempo { get; private set; } // dias, meses, anios

    // ==========================================
    // 3. DETALLES (Líneas de la factura)
    // ==========================================
    public Comprador Cliente { get; private init; } = null!;
    public Guid? CatalogoClienteId { get; private set; }
    public IReadOnlyCollection<DetalleFactura> Detalles => _detalles.AsReadOnly();
    private List<DetalleFactura> _detalles = [];

    // ==========================================
    // 4. INFORMACIÓN ADICIONAL DINÁMICA (SRI)
    // ==========================================
    public IReadOnlyCollection<CampoAdicional> CamposAdicionales => _camposAdicionales.AsReadOnly();
    private List<CampoAdicional> _camposAdicionales = [];

    // Constructor privado para requerir el uso de un Factory Method (DDD)
    private Factura() { }

    // Método de creación
    public static Factura Crear(
        Guid tenantId, int ambiente, string razonSocial, string rucEmisor, 
        string establecimiento, string puntoEmision, string secuencial, 
        string direccionMatriz, DateTime fechaEmision, Comprador cliente, 
        List<DetalleFactura> detalles, int emisorId = 0,
        string? contribuyenteRimpe = null,
        Guid? catalogoClienteId = null,
        List<CampoAdicional>? camposAdicionales = null,
        string? formaPago = "01",
        decimal? plazo = null,
        string? unidadTiempo = null)
    {
        var codigoFormaPago = string.IsNullOrWhiteSpace(formaPago) ? "01" : formaPago.Trim();
        var factura = new Factura
        {
            TenantId = tenantId,
            EmisorId = emisorId,
            Ambiente = ambiente,
            TipoEmision = 1, // Para el método de autorización offline, solo existe el tipo de emisión normal (1)[cite: 3].
            RazonSocial = razonSocial,
            Ruc = rucEmisor,
            CodDoc = "01", // El código que identifica a la FACTURA es obligatoriamente 01[cite: 3].
            Establecimiento = establecimiento,
            PuntoEmision = puntoEmision,
            Secuencial = secuencial,
            DireccionMatriz = direccionMatriz,
            ContribuyenteRimpe = Constants.RegimenRimpeTipos.Normalizar(contribuyenteRimpe),
            FechaEmision = fechaEmision,
            Estado = "CREADA",
            CatalogoClienteId = catalogoClienteId,
            FormaPago = codigoFormaPago,
            Plazo = plazo > 0 ? plazo : null,
            UnidadTiempo = plazo > 0 ? (string.IsNullOrWhiteSpace(unidadTiempo) ? "dias" : unidadTiempo.Trim().ToLowerInvariant()) : null,
        
            // Delegamos toda la información del adquirente al objeto compuesto
            Cliente = cliente,
            TipoIdentificacionComprador = cliente.TipoIdentificacion,
            RazonSocialComprador = cliente.RazonSocial,
            IdentificacionComprador = cliente.Identificacion,
            _detalles = detalles,
            _camposAdicionales = camposAdicionales ?? [],
            ClaveAcceso = string.Empty
        };

        factura.CalcularTotales();
        factura.ValidarInvariantes(); // Aquí se validará automáticamente la regla de los $50 USD[cite: 3].
    
        // Si estás utilizando Domain Events, aquí registrarías el evento antes de retornar:
        // factura.AddDomainEvent(new FacturaEmitidaEvent(factura));

        return factura;
    }

    public void AsignarCatalogoCliente(Guid catalogoClienteId) => CatalogoClienteId = catalogoClienteId;

    public void MarcarComoRecibida()
    {
        Estado = "RECIBIDA";
        MensajeErrorSri = null;
    }

    public void MarcarComoAutorizada(string numeroAutorizacion, DateTime fechaAutorizacion, string? comprobanteXml = null)
    {
        if (string.IsNullOrWhiteSpace(numeroAutorizacion))
            throw new ArgumentException("El número de autorización es obligatorio.", nameof(numeroAutorizacion));

        Estado = "AUTORIZADO";
        NumeroAutorizacion = numeroAutorizacion;
        FechaAutorizacion = fechaAutorizacion;
        MensajeErrorSri = null;

        if (!string.IsNullOrWhiteSpace(comprobanteXml))
        {
            XmlFirmado = comprobanteXml;
        }
    }

    public void AsignarXmlFirmado(string xmlFirmado)
    {
        if (!string.IsNullOrWhiteSpace(xmlFirmado))
        {
            XmlFirmado = xmlFirmado;
        }
    }

    public void MarcarComoDevuelta(string motivoError)
    {
        Estado = "DEVUELTA";
        MensajeErrorSri = motivoError;
    }

    public void MarcarComoNoAutorizada(string motivoError)
    {
        Estado = "NO AUTORIZADO";
        MensajeErrorSri = motivoError;
    }

    private void CalcularTotales()
    {
        TotalSinImpuestos = _detalles.Sum(d => d.PrecioTotalSinImpuesto);
        TotalDescuento = _detalles.Sum(d => d.Descuento);
        ImporteTotal = (TotalSinImpuestos - TotalDescuento) + _detalles.Sum(d => d.TotalImpuestos);
    }

    private void ValidarInvariantes()
    {
        // Bloqueo a consumidor final si supera los $50.00 USD (SRI: hasta $50.00 permitido a Consumidor Final)
        if (ImporteTotal > 50.00m && Cliente.EsConsumidorFinal())
        {
            throw new InvalidOperationException("El importe supera los $50.00 USD. Se requieren datos del adquirente.");
        }
    }

    public void AsignarClaveAcceso(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Length != 49)
        {
            throw new ArgumentException("La clave de acceso debe tener exactamente 49 dígitos.");
        }

        ClaveAcceso = clave;
    }

    // ==========================================
    // REGLAS DE NEGOCIO (Validaciones Invariantes)
    // ==========================================
    public void ValidarConsumidorFinal()
    {
        // Si el valor de la factura es mayor a 50 USD se deberá especificar obligatoriamente los datos del adquirente.
        if (ImporteTotal > 50.00m && IdentificacionComprador == "9999999999999")
        {
            throw new InvalidOperationException(
                "Para facturas mayores a 50 USD es obligatorio especificar los datos del adquirente, no se permite Consumidor Final.");
        }
    }

    public void AgregarCampoAdicional(string nombre, string valor)
    {
        if (_camposAdicionales.Count >= 15)
        {
            throw new InvalidOperationException("El SRI permite un máximo de 15 campos adicionales por comprobante.");
        }

        _camposAdicionales.Add(CampoAdicional.Crear(nombre, valor));
    }
}
