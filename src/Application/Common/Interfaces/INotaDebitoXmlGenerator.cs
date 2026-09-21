using System.Xml.Linq;
using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface INotaDebitoXmlGenerator
{
    XDocument GenerarXml(NotaDebito notaDebito);
    byte[] GenerarXmlBytes(NotaDebito notaDebito);
    byte[] GenerarXmlAutorizadoBytes(NotaDebito notaDebito, string? xmlComprobanteFirmado = null);
}

