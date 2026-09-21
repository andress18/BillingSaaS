namespace BillingSaaS.Domain.Entities;

using System;
using System.Collections.Generic;
using System.Linq;

public class NotaDebito : BaseAuditableEntity
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

    /// <summary>
    /// 1: Pruebas, 2: Producción
    /// </summary>
    public int Ambiente { get; private set; }

    /// <summary>
    /// 1: Emisión Normal
    /// </summary>
    public int TipoEmision { get; private set; } = 1;

    /// <summary>
    /// Razón Social del emisor
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
    /// 05 para Nota de Débito
    /// </summary>
    public string CodDoc { get; private set; } = "05";

    /// <summary>
    /// 3 dígitos (ej: 001)
    /// </summary>
    public string Establecimiento { get; private set; } = null!;

    /// <summary>
    /// 3 dígitos (ej: 001)
    /// </summary>
    public string PuntoEmision { get; private set; } = null!;

    /// <summary>
    /// 9 dígitos (ej: 000000001)
    /// </summary>
    public string Secuencial { get; private set; } = null!;

    /// <summary>
    /// Dirección matriz del emisor
    /// </summary>
    public string DireccionMatriz { get; private set; } = null!;

    /// <summary>
    /// Leyenda oficial SRI para RIMPE si aplica
    /// </summary>
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

    // ==========================================
    // 3. INFORMACIÓN DE LA NOTA DE DÉBITO
    // ==========================================

    public DateTime FechaEmision { get; private set; }
    public string TipoIdentificacionComprador { get; private set; } = null!;
    public string RazonSocialComprador { get; private set; } = null!;
    public string IdentificacionComprador { get; private set; } = null!;

    public decimal TotalSinImpuestos { get; private set; }
    public decimal ValorTotal { get; private set; }

    // ==========================================
    // 4. COLECCIONES Y RELACIONES
    // ==========================================

    public Comprador Cliente { get; private init; } = null!;
    public int? ClienteId { get; private set; }

    public IReadOnlyCollection<MotivoNotaDebito> Motivos => _motivos.AsReadOnly();
    private List<MotivoNotaDebito> _motivos = [];

    public IReadOnlyCollection<ImpuestoNotaDebito> Impuestos => _impuestos.AsReadOnly();
    private List<ImpuestoNotaDebito> _impuestos = [];

    public IReadOnlyCollection<PagoNotaDebito> Pagos => _pagos.AsReadOnly();
    private List<PagoNotaDebito> _pagos = [];

    private NotaDebito() { }

    public static NotaDebito Crear(
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
        List<MotivoNotaDebito> motivos,
        List<ImpuestoNotaDebito> impuestos,
        List<PagoNotaDebito>? pagos = null,
        string codDocModificado = "01",
        int emisorId = 0,
        string? contribuyenteRimpe = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(rucEmisor) || rucEmisor.Length != 13)
            throw new ArgumentException("El RUC del emisor debe tener 13 dígitos.", nameof(rucEmisor));

        if (string.IsNullOrWhiteSpace(numDocModificado) || !System.Text.RegularExpressions.Regex.IsMatch(numDocModificado, @"^\d{3}-\d{3}-\d{9}$"))
            throw new ArgumentException("El número de comprobante modificado debe tener formato 001-001-000000001.", nameof(numDocModificado));

        if (motivos == null || motivos.Count == 0)
            throw new ArgumentException("La nota de débito debe contener al menos un motivo.", nameof(motivos));

        var notaDebito = new NotaDebito
        {
            TenantId = tenantId,
            EmisorId = emisorId,
            Ambiente = ambiente,
            TipoEmision = 1,
            RazonSocial = razonSocial,
            Ruc = rucEmisor,
            CodDoc = "05",
            Establecimiento = establecimiento,
            PuntoEmision = puntoEmision,
            Secuencial = secuencial,
            DireccionMatriz = direccionMatriz,
            ContribuyenteRimpe = Constants.RegimenRimpeTipos.Normalizar(contribuyenteRimpe),
            FechaEmision = fechaEmision,
            Estado = "CREADA",

            CodDocModificado = codDocModificado,
            NumDocModificado = numDocModificado,
            FechaEmisionDocSustento = fechaEmisionDocSustento,

            Cliente = cliente,
            TipoIdentificacionComprador = cliente.TipoIdentificacion,
            RazonSocialComprador = cliente.RazonSocial,
            IdentificacionComprador = cliente.Identificacion,

            _motivos = motivos,
            _impuestos = impuestos ?? [],
            _pagos = pagos ?? [],
            ClaveAcceso = string.Empty
        };

        notaDebito.CalcularTotales();
        return notaDebito;
    }

    private void CalcularTotales()
    {
        TotalSinImpuestos = decimal.Round(_motivos.Sum(m => m.Valor), 2, MidpointRounding.AwayFromZero);
        var totalImpuestos = decimal.Round(_impuestos.Sum(i => i.Valor), 2, MidpointRounding.AwayFromZero);
        ValorTotal = TotalSinImpuestos + totalImpuestos;

        // Si no se proporcionaron pagos explícitos, generar pago por defecto de contado (01)
        if (_pagos.Count == 0)
        {
            _pagos.Add(PagoNotaDebito.Crear("01", ValorTotal));
        }
    }

    public void AsignarClaveAcceso(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Length != 49)
            throw new ArgumentException("La clave de acceso debe tener exactamente 49 dígitos.", nameof(clave));

        ClaveAcceso = clave;
    }

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
}

