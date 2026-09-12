using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

namespace BillingSaaS.Infrastructure.Servicios
{
    public class SriSignatureService
    {
        public XmlDocument FirmarXml(XDocument xmlSinFirma, byte[] p12Bytes, string passwordP12)
        {
            // Carga segura del archivo .p12 utilizando X509CertificateLoader
            using var certificado = X509CertificateLoader.LoadPkcs12(p12Bytes, passwordP12);

            var rsaKey = certificado.GetRSAPrivateKey();
            if (rsaKey == null)
                throw new CryptographicException("El certificado no contiene una clave RSA privada válida.");

            // Convertir XDocument a XmlDocument requerido por SignedXml
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            using (var reader = xmlSinFirma.CreateReader())
            {
                xmlDoc.Load(reader);
            }

            // Inicializar SignedXml con el algoritmo RSA-SHA1[cite: 2]
            var signedXml = new SignedXml(xmlDoc) { SigningKey = rsaKey };
            signedXml.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;

            // Tipo de firma ENVELOPED referenciando el id 'comprobante'[cite: 2]
            var reference = new Reference { Uri = "#comprobante" };
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.DigestMethod = SignedXml.XmlDsigSHA1Url; 
            signedXml.AddReference(reference);

            // Construcción del nodo KeyInfo conteniendo el certificado X509 en base64[cite: 2]
            var keyInfo = new KeyInfo();
            keyInfo.Id = $"Certificate-{Guid.NewGuid():N}";
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            
            // Inyección del módulo y exponente RSA[cite: 2]
            var rsaKeyValue = new RSAKeyValue(rsaKey);
            keyInfo.AddClause(rsaKeyValue);
            signedXml.KeyInfo = keyInfo;

            // Inyección del nodo XAdES-BES (QualifyingProperties) versión 1.3.2[cite: 2]
            var xadesObject = CrearNodoXadesBes(certificado, reference.Uri, keyInfo.Id);
            signedXml.AddObject(xadesObject);

            signedXml.ComputeSignature();
            var xmlFirma = signedXml.GetXml();

            xmlDoc.DocumentElement!.AppendChild(xmlDoc.ImportNode(xmlFirma, true));

            return xmlDoc;
        }

        private DataObject CrearNodoXadesBes(X509Certificate2 cert, string documentReferenceUri, string keyInfoId)
        {
            // La fecha de la firma no debe ser posterior a la actual; se recomienda sincronizar con 0.south-america.pool.ntp.org[cite: 2]
            var signingTime = DateTime.UtcNow.AddHours(-5).ToString("yyyy-MM-ddTHH:mm:sszzz");

            var certHash = cert.GetCertHash(HashAlgorithmName.SHA1);
            var certDigestBase64 = Convert.ToBase64String(certHash);

            string xadesNamespace = "http://uri.etsi.org/01903/v1.3.2#";

            var xadesXml = $@"
            <etsi:QualifyingProperties Target=""#Signature-{Guid.NewGuid():N}"" xmlns:etsi=""{xadesNamespace}"">
                <etsi:SignedProperties Id=""Signature-SignedProperties"">
                    <etsi:SignedSignatureProperties>
                        <etsi:SigningTime>{signingTime}</etsi:SigningTime>
                        <etsi:SigningCertificate>
                            <etsi:Cert>
                                <etsi:CertDigest>
                                    <ds:DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1"" xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"" />
                                    <ds:DigestValue xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{certDigestBase64}</ds:DigestValue>
                                </etsi:CertDigest>
                                <etsi:IssuerSerial>
                                    <ds:X509IssuerName xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{cert.Issuer}</ds:X509IssuerName>
                                    <ds:X509SerialNumber xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{GetDecimalSerialNumber(cert)}</ds:X509SerialNumber>
                                </etsi:IssuerSerial>
                            </etsi:Cert>
                        </etsi:SigningCertificate>
                    </etsi:SignedSignatureProperties>
                    <etsi:SignedDataObjectProperties>
                        <etsi:DataObjectFormat ObjectReference=""{documentReferenceUri}"">
                            <etsi:Description>contenido comprobante</etsi:Description>
                            <etsi:MimeType>text/xml</etsi:MimeType>
                        </etsi:DataObjectFormat>
                    </etsi:SignedDataObjectProperties>
                </etsi:SignedProperties>
            </etsi:QualifyingProperties>";

            var doc = new XmlDocument();
            doc.LoadXml(xadesXml);

            return new DataObject
            {
                Id = $"Signature-Object-{Guid.NewGuid():N}",
                Data = doc.DocumentElement?.SelectNodes(".")!
            };
        }

        private string GetDecimalSerialNumber(X509Certificate2 cert)
        {
            var serialBytes = cert.GetSerialNumber();
            Array.Reverse(serialBytes); 
            return new System.Numerics.BigInteger(serialBytes).ToString();
        }
    }
}
