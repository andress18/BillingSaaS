namespace BillingSaaS.Infrastructure.Servicios.Sri;

public static class SriEndpoints
{
    // Ambiente 1: Pruebas / Certificación
    public const string RecepcionPruebas = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
    public const string AutorizacionPruebas = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

    // Ambiente 2: Producción
    public const string RecepcionProduccion = "https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline";
    public const string AutorizacionProduccion = "https://cel.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";

    public static string ObtenerUrlRecepcion(int ambiente) =>
        ambiente switch
        {
            1 => RecepcionPruebas,
            2 => RecepcionProduccion,
            _ => throw new ArgumentOutOfRangeException(nameof(ambiente), $"Ambiente {ambiente} no soportado. Valores válidos: 1 (Pruebas), 2 (Producción).")
        };

    public static string ObtenerUrlAutorizacion(int ambiente) =>
        ambiente switch
        {
            1 => AutorizacionPruebas,
            2 => AutorizacionProduccion,
            _ => throw new ArgumentOutOfRangeException(nameof(ambiente), $"Ambiente {ambiente} no soportado. Valores válidos: 1 (Pruebas), 2 (Producción).")
        };
}

