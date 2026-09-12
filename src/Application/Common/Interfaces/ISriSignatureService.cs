using System.Xml;
using System.Xml.Linq;

namespace BillingSaaS.Application.Common.Interfaces;

public interface ISriSignatureService
{
    XmlDocument FirmarXml(XDocument xmlSinFirma, byte[] p12Bytes, string passwordP12);
}

