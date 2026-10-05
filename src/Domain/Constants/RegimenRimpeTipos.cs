using System;
using System.Linq;

namespace BillingSaaS.Domain.Constants;

public static class RegimenRimpeTipos
{
    // Leyendas oficiales exactas exigidas por la Ficha Técnica y Resoluciones del SRI
    public const string General = "CONTRIBUYENTE RÉGIMEN GENERAL";
    public const string Emprendedor = "CONTRIBUYENTE RÉGIMEN RIMPE";
    public const string NegocioPopular = "CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE";

    // Códigos estándar utilizados comúnmente en interfaces y sistemas contables
    public const string CodigoGeneral = "GENERAL";
    public const string CodigoEmprendedor = "RIMPE_EMPRENDEDOR";
    public const string CodigoNegocioPopular = "RIMPE_NEGOCIO_POPULAR";

    public static readonly string[] OpcionesValidas =
    [
        General,
        Emprendedor,
        NegocioPopular
    ];

    /// <summary>
    /// Determina si un texto de régimen corresponde a alguno de los regímenes RIMPE (Emprendedor o Negocio Popular).
    /// </summary>
    public static bool EsRimpe(string? regimen)
    {
        if (string.IsNullOrWhiteSpace(regimen))
            return false;

        var upper = regimen.Trim().ToUpperInvariant();
        return upper.Contains("RIMPE") || upper.Contains("POPULAR") || upper.Contains("EMPRENDEDOR");
    }

    /// <summary>
    /// Resuelve la leyenda oficial para la representación impresa (RIDE) y campos adicionales.
    /// Si es nulo, vacío, general o desconocido, retorna por defecto "CONTRIBUYENTE RÉGIMEN GENERAL".
    /// </summary>
    public static string ResolverParaRide(string? regimen)
    {
        if (string.IsNullOrWhiteSpace(regimen))
            return General;

        var clean = regimen.Trim();
        var upper = clean.ToUpperInvariant();

        if (upper.Contains("POPULAR") || upper is CodigoNegocioPopular or "NEGOCIO_POPULAR")
            return NegocioPopular;

        if (upper.Contains("RIMPE") || upper.Contains("EMPRENDEDOR") || upper is CodigoEmprendedor or "EMPRENDEDOR")
            return Emprendedor;

        return General;
    }

    /// <summary>
    /// Normaliza cualquier entrada del usuario/frontend al formato legal oficial exigido por el SRI.
    /// Si corresponde al Régimen General, devuelve General ("CONTRIBUYENTE RÉGIMEN GENERAL").
    /// </summary>
    public static string? Normalizar(string? regimen)
    {
        if (string.IsNullOrWhiteSpace(regimen))
            return null;

        var clean = regimen.Trim();
        var upper = clean.ToUpperInvariant();

        if (upper is "GENERAL" or "NINGUNO" or "NO" or "FALSE" or "NONE" or "NO APLICA" or "0" or "CONTRIBUYENTE RÉGIMEN GENERAL" or "CONTRIBUYENTE REGIMEN GENERAL" or "RÉGIMEN GENERAL" or "REGIMEN GENERAL")
            return General;

        if (upper.Contains("POPULAR") || upper is CodigoNegocioPopular or "NEGOCIO_POPULAR")
            return NegocioPopular;

        if (upper.Contains("RIMPE") || upper.Contains("EMPRENDEDOR") || upper is CodigoEmprendedor or "EMPRENDEDOR")
            return Emprendedor;

        if (upper.Contains("GENERAL"))
            return General;

        return clean;
    }

    /// <summary>
    /// Verifica si un texto de régimen corresponde a un valor legal permitido por el SRI o es nulo/general.
    /// </summary>
    public static bool EsValido(string? regimen)
    {
        if (string.IsNullOrWhiteSpace(regimen))
            return true;

        var normalizado = Normalizar(regimen);
        return normalizado is null or General or Emprendedor or NegocioPopular;
    }
}

