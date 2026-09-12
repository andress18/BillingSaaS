namespace BillingSaaS.Domain.Entities;

using System;
using System.Collections.Generic;
using System.Linq;

public class Factura : BaseAuditableEntity
{
    // 1. Aislamiento Multi-tenant (SaaS)
    public Guid TenantId { get; private set; }

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
    // 3. DETALLES (Líneas de la factura)
    // ==========================================
    public Comprador Cliente { get; private init; } = null!;
    public IReadOnlyCollection<DetalleFactura> Detalles => _detalles.AsReadOnly();
    private List<DetalleFactura> _detalles = [];

    // Constructor privado para requerir el uso de un Factory Method (DDD)
    private Factura() { }

    // Método de creación
    public static Factura Crear(
        Guid tenantId, int ambiente, string razonSocial, string rucEmisor, 
        string establecimiento, string puntoEmision, string secuencial, 
        string direccionMatriz, DateTime fechaEmision, Comprador cliente, 
        List<DetalleFactura> detalles)
    {
        var factura = new Factura
        {
            TenantId = tenantId,
            Ambiente = ambiente,
            TipoEmision = 1, // Para el método de autorización offline, solo existe el tipo de emisión normal (1)[cite: 3].
            RazonSocial = razonSocial,
            Ruc = rucEmisor,
            CodDoc = "01", // El código que identifica a la FACTURA es obligatoriamente 01[cite: 3].
            Establecimiento = establecimiento,
            PuntoEmision = puntoEmision,
            Secuencial = secuencial,
            DireccionMatriz = direccionMatriz,
            FechaEmision = fechaEmision,
        
            // Delegamos toda la información del adquirente al objeto compuesto
            Cliente = cliente,
            _detalles = detalles,
            ClaveAcceso = string.Empty
        };

        factura.CalcularTotales();
        factura.ValidarInvariantes(); // Aquí se validará automáticamente la regla de los $50 USD[cite: 3].
    
        // Si estás utilizando Domain Events, aquí registrarías el evento antes de retornar:
        // factura.AddDomainEvent(new FacturaEmitidaEvent(factura));

        return factura;
    }

    private void CalcularTotales()
    {
        TotalSinImpuestos = _detalles.Sum(d => d.PrecioTotalSinImpuesto);
        TotalDescuento = _detalles.Sum(d => d.Descuento);
        ImporteTotal = (TotalSinImpuestos - TotalDescuento) + _detalles.Sum(d => d.TotalImpuestos);
    }

    private void ValidarInvariantes()
    {
        // Bloqueo a consumidor final si supera $50
        if (ImporteTotal >= 50.00m && Cliente.EsConsumidorFinal())
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
}
