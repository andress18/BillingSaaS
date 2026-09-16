using System.Xml.Linq;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IFacturaXmlGenerator
{
    XDocument GenerarXml(Factura factura);
    byte[] GenerarXmlBytes(Factura factura);
    byte[] GenerarXmlAutorizadoBytes(Factura factura, string? xmlComprobanteFirmado = null);
}

