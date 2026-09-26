namespace BillingSaaS.Domain.Constants;

public static class FormasPagoSRI
{
    public const string SinUtilizacionSistemaFinanciero = "01";
    public const string CompensacionDeudas = "15";
    public const string TarjetaDebito = "16";
    public const string DineroElectronico = "17";
    public const string TarjetaPrepago = "18";
    public const string TarjetaCredito = "19";
    public const string OtrosConUtilizacionSistemaFinanciero = "20";
    public const string EndosoTitulos = "21";

    public static readonly Dictionary<string, string> Descripciones = new()
    {
        { SinUtilizacionSistemaFinanciero, "01 - SIN UTILIZACION DEL SISTEMA FINANCIERO" },
        { CompensacionDeudas, "15 - COMPENSACION DE DEUDAS" },
        { TarjetaDebito, "16 - TARJETA DE DEBITO" },
        { DineroElectronico, "17 - DINERO ELECTRONICO" },
        { TarjetaPrepago, "18 - TARJETA PREPAGO" },
        { TarjetaCredito, "19 - TARJETA DE CREDITO" },
        { OtrosConUtilizacionSistemaFinanciero, "20 - OTROS CON UTILIZACION DEL SISTEMA FINANCIERO" },
        { EndosoTitulos, "21 - ENDOSO DE TITULOS" }
    };

    public static string ObtenerDescripcion(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Descripciones[SinUtilizacionSistemaFinanciero];

        return Descripciones.TryGetValue(codigo, out var desc)
            ? desc
            : $"{codigo} - OTROS";
    }

    public static bool EsValido(string? codigo)
    {
        return !string.IsNullOrWhiteSpace(codigo) && Descripciones.ContainsKey(codigo);
    }
}

