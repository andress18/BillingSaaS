using System.Xml.Linq;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface INotaCreditoXmlGenerator
{
    XDocument GenerarXml(NotaCredito notaCredito);
    byte[] GenerarXmlBytes(NotaCredito notaCredito);
    byte[] GenerarXmlAutorizadoBytes(NotaCredito notaCredito, string? xmlComprobanteFirmado = null);
}

