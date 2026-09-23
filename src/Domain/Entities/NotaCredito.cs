namespace BillingSaaS.Domain.Entities;

using System;
using System.Collections.Generic;
using System.Linq;

public class NotaCredito : BaseAuditableEntity
{
    // 1. Aislamiento Multi-tenant (SaaS)
    public Guid TenantId { get; private set; }

    // Relación con el Emisor
    public int EmisorId { get; private set; }

    // Estado del comprobante en el flujo SRI (CREADA, RECIBIDA, AUTORIZADA, DEVUELTA, NO AUTORIZADO)
    public string Estado { get; private set; } = "CREADA";
    public string? NumeroAutorizacion { get; private set; }
    public DateTime? FechaAutorizacion { get; private set; }
    public string? MensajeErrorSri { get; private set; }
    public string? XmlFirmado { get; private set; }

    // ==========================================
    // 1. INFORMACIÓN TRIBUTARIA (Cabecera)
    // ==========================================

    public int Ambiente { get; private set; }
    public int TipoEmision { get; private set; } = 1;
    public string RazonSocial { get; private set; } = null!;
    public string Ruc { get; private set; } = null!;
    public string ClaveAcceso { get; private set; } = null!;

    /// <summary>
    /// 04 para Nota de Crédito
    /// </summary>
    public string CodDoc { get; private set; } = "04";

    public string Establecimiento { get; private set; } = null!;
    public string PuntoEmision { get; private set; } = null!;
    public string Secuencial { get; private set; } = null!;
    public string DireccionMatriz { get; private set; } = null!;
    public string? ContribuyenteRimpe { get; private set; }

    // ==========================================
    // 2. DOCUMENTO MODIFICADO (Sustento)
    // ==========================================

    /// <summary>
    /// Código del comprobante modificado (01: Factura)
    /// </summary>
    public string CodDocModificado { get; private set; } = "01";

    /// <summary>
    /// Formato: 001-001-000000123
    /// </summary>
    public string NumDocModificado { get; private set; } = null!;

    /// <summary>
    /// Fecha de emisión del documento original que se modifica
    /// </summary>
    public DateTime FechaEmisionDocSustento { get; private set; }

    /// <summary>
    /// Motivo o razón de la modificación (hasta 300 caracteres)
    /// </summary>
    public string Motivo { get; private set; } = null!;

    // ==========================================
    // 3. INFORMACIÓN DE LA NOTA DE CRÉDITO
    // ==========================================

    public DateTime FechaEmision { get; private set; }
    public string TipoIdentificacionComprador { get; private set; } = null!;
    public string RazonSocialComprador { get; private set; } = null!;
    public string IdentificacionComprador { get; private set; } = null!;

    public decimal TotalSinImpuestos { get; private set; }
    public decimal TotalDescuento { get; private set; }
    public decimal ValorModificacion { get; private set; }

    // ==========================================
    // 4. COLECCIONES Y RELACIONES
    // ==========================================

    public Comprador Cliente { get; private init; } = null!;
    public int? ClienteId { get; private set; }
    public Guid? CatalogoClienteId { get; private set; }

    public IReadOnlyCollection<DetalleNotaCredito> Detalles => _detalles.AsReadOnly();
    private List<DetalleNotaCredito> _detalles = [];

    private NotaCredito() { }

    public static NotaCredito Crear(
        Guid tenantId,
        int ambiente,
        string razonSocial,
        string rucEmisor,
        string establecimiento,
        string puntoEmision,
        string secuencial,
        string direccionMatriz,
        DateTime fechaEmision,
        Comprador cliente,
        string numDocModificado,
        DateTime fechaEmisionDocSustento,
        string motivo,
        List<DetalleNotaCredito> detalles,
        string codDocModificado = "01",
        int emisorId = 0,
        string? contribuyenteRimpe = null,
        Guid? catalogoClienteId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(rucEmisor) || rucEmisor.Length != 13)
            throw new ArgumentException("El RUC del emisor debe tener 13 dígitos.", nameof(rucEmisor));

        if (string.IsNullOrWhiteSpace(numDocModificado) || !System.Text.RegularExpressions.Regex.IsMatch(numDocModificado, @"^\d{3}-\d{3}-\d{9}$"))
            throw new ArgumentException("El número de comprobante modificado debe tener formato 001-001-000000001.", nameof(numDocModificado));

        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 300)
            throw new ArgumentException("El motivo de la modificación es obligatorio y debe tener máximo 300 caracteres.", nameof(motivo));

        if (detalles == null || detalles.Count == 0)
            throw new ArgumentException("La nota de crédito debe contener al menos un detalle.", nameof(detalles));

        var notaCredito = new NotaCredito
        {
            TenantId = tenantId,
            EmisorId = emisorId,
            Ambiente = ambiente,
            TipoEmision = 1,
            RazonSocial = razonSocial,
            Ruc = rucEmisor,
            CodDoc = "04",
            Establecimiento = establecimiento,
            PuntoEmision = puntoEmision,
            Secuencial = secuencial,
            DireccionMatriz = direccionMatriz,
            ContribuyenteRimpe = Constants.RegimenRimpeTipos.Normalizar(contribuyenteRimpe),
            FechaEmision = fechaEmision,
            Estado = "CREADA",
            Cliente = cliente,
            TipoIdentificacionComprador = cliente.TipoIdentificacion,
            RazonSocialComprador = cliente.RazonSocial,
            IdentificacionComprador = cliente.Identificacion,
            CodDocModificado = codDocModificado,
            NumDocModificado = numDocModificado,
            FechaEmisionDocSustento = fechaEmisionDocSustento,
            Motivo = motivo,
            CatalogoClienteId = catalogoClienteId,
            _detalles = detalles
        };

        notaCredito.CalcularTotales();
        return notaCredito;
    }

    public void AsignarClaveAcceso(string claveAcceso)
    {
        if (string.IsNullOrWhiteSpace(claveAcceso) || claveAcceso.Length != 49)
            throw new ArgumentException("La clave de acceso debe tener 49 dígitos.", nameof(claveAcceso));

        ClaveAcceso = claveAcceso;
    }

    public void AsignarXmlFirmado(string xmlFirmado)
    {
        if (!string.IsNullOrWhiteSpace(xmlFirmado))
            XmlFirmado = xmlFirmado;
    }

    public void MarcarComoRecibida()
    {
        Estado = "RECIBIDA";
        MensajeErrorSri = null;
    }

    public void MarcarComoAutorizada(string numeroAutorizacion, DateTime fechaAutorizacion, string? comprobanteXml = null)
    {
        Estado = "AUTORIZADO";
        NumeroAutorizacion = numeroAutorizacion;
        FechaAutorizacion = fechaAutorizacion;
        MensajeErrorSri = null;

        if (!string.IsNullOrWhiteSpace(comprobanteXml))
            XmlFirmado = comprobanteXml;
    }

    public void MarcarComoDevuelta(string mensajeError)
    {
        Estado = "DEVUELTA";
        MensajeErrorSri = mensajeError;
    }

    public void MarcarComoNoAutorizada(string mensajeError)
    {
        Estado = "NO AUTORIZADO";
        MensajeErrorSri = mensajeError;
    }

    private void CalcularTotales()
    {
        TotalSinImpuestos = _detalles.Sum(d => d.PrecioTotalSinImpuesto);
        TotalDescuento = _detalles.Sum(d => d.Descuento);
        ValorModificacion = TotalSinImpuestos + _detalles.Sum(d => d.TotalImpuestos);
    }
}
