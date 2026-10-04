using BillingSaaS.MigrationTool.Models;

namespace BillingSaaS.MigrationTool.Services;

public interface IXmlFacturaParser
{
    /// <summary>
    /// Parsea un XML (en claro, firmado o envuelto en respuesta de autorización del SRI)
    /// y retorna los datos estructurados en FacturaMigracionDto.
    /// </summary>
    FacturaMigracionDto ParsearFacturaXml(string xmlContent, string? estadoPorDefecto = null);

    /// <summary>
    /// Extrae datos tributarios y el último secuencial a partir del conjunto de XMLs del cliente.
    /// </summary>
    void EnriquecerEmisorDesdeFacturas(ClienteMigracionDto cliente);
}
