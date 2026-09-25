using System;
using System.Linq;

namespace BillingSaaS.Domain.Entities;

public class Plan : BaseAuditableEntity
{
    public string Codigo { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;
    public string Descripcion { get; private set; } = null!;
    public decimal PrecioMensual { get; private set; }
    public decimal PrecioAnual { get; private set; }
    public int? MaxDocumentosMensuales { get; private set; }
    public int? MaxDocumentosAnuales { get; private set; }
    public int MaxEstablecimientos { get; private set; }
    public string TiposDocumentosPermitidos { get; private set; } = "01"; // Comma-separated: 01 (Factura), 04 (Nota Credito), 05 (Nota Debito), 06 (Guia Remision), 03 (Liquidacion)
    public bool EsPublico { get; private set; } = true;
    public bool Activo { get; private set; } = true;

    private Plan() { }

    public static Plan Crear(
        string codigo,
        string nombre,
        string descripcion,
        decimal precioMensual,
        decimal precioAnual,
        int? maxDocumentosMensuales,
        int? maxDocumentosAnuales,
        int maxEstablecimientos,
        string tiposDocumentosPermitidos,
        bool esPublico = true,
        int id = 0)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código del plan es obligatorio.", nameof(codigo));

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del plan es obligatorio.", nameof(nombre));

        var plan = new Plan
        {
            Id = id,
            Codigo = codigo.Trim().ToUpperInvariant(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim() ?? string.Empty,
            PrecioMensual = precioMensual,
            PrecioAnual = precioAnual,
            MaxDocumentosMensuales = maxDocumentosMensuales,
            MaxDocumentosAnuales = maxDocumentosAnuales,
            MaxEstablecimientos = Math.Max(1, maxEstablecimientos),
            TiposDocumentosPermitidos = string.IsNullOrWhiteSpace(tiposDocumentosPermitidos) ? "01" : tiposDocumentosPermitidos.Trim(),
            EsPublico = esPublico,
            Activo = true
        };

        return plan;
    }

    public bool PermiteTipoDocumento(string codDoc)
    {
        if (string.IsNullOrWhiteSpace(codDoc)) return false;
        var permitidos = TiposDocumentosPermitidos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return permitidos.Contains(codDoc);
    }

    public bool PermiteEstablecimientos(int cantidad)
    {
        return cantidad <= MaxEstablecimientos;
    }

    public bool EsIlimitado(string frecuencia)
    {
        return frecuencia.ToUpperInvariant() switch
        {
            "ANUAL" => !MaxDocumentosAnuales.HasValue || MaxDocumentosAnuales.Value <= 0,
            _ => !MaxDocumentosMensuales.HasValue || MaxDocumentosMensuales.Value <= 0
        };
    }

    public int? ObtenerLimiteDocumentos(string frecuencia)
    {
        return frecuencia.ToUpperInvariant() switch
        {
            "ANUAL" => MaxDocumentosAnuales,
            _ => MaxDocumentosMensuales
        };
    }

    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;

    public void ActualizarPrecios(decimal precioMensual, decimal precioAnual)
    {
        PrecioMensual = Math.Max(0m, precioMensual);
        PrecioAnual = Math.Max(0m, precioAnual);
    }

    public void ActualizarLimites(int? maxMensuales, int? maxAnuales, int maxEstablecimientos, string tiposDocumentosPermitidos)
    {
        MaxDocumentosMensuales = maxMensuales;
        MaxDocumentosAnuales = maxAnuales;
        MaxEstablecimientos = Math.Max(1, maxEstablecimientos);
        TiposDocumentosPermitidos = string.IsNullOrWhiteSpace(tiposDocumentosPermitidos) ? "01" : tiposDocumentosPermitidos.Trim();
    }
}

