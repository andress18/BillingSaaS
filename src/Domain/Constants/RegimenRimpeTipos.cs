using System;
using System.Linq;

namespace BillingSaaS.Domain.Constants;

public static class RegimenRimpeTipos
{
    // Leyendas oficiales exactas exigidas por la Ficha Técnica y Resoluciones del SRI
    public const string General = "GENERAL";
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
    /// Normaliza cualquier entrada del usuario/frontend al formato legal oficial exigido por el SRI,
    /// o devuelve null si corresponde al Régimen General (sin leyenda RIMPE).
    /// </summary>
    public static string? Normalizar(string? regimen)
    {
        if (string.IsNullOrWhiteSpace(regimen))
            return null;

        var clean = regimen.Trim();
        var upper = clean.ToUpperInvariant();

        if (upper is "GENERAL" or "NINGUNO" or "NO" or "FALSE" or "NONE" or "NO APLICA" or "0")
            return null;

        if (upper.Contains("POPULAR") || upper is CodigoNegocioPopular or "NEGOCIO_POPULAR")
            return NegocioPopular;

        if (upper.Contains("RIMPE") || upper.Contains("EMPRENDEDOR") || upper is CodigoEmprendedor or "EMPRENDEDOR")
            return Emprendedor;

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
        return normalizado is null or Emprendedor or NegocioPopular;
    }
}

