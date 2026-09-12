using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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

            // Identificadores consistentes para la firma XAdES-BES
            var signatureId = $"Signature-{Guid.NewGuid():N}";
            var keyInfoId = $"Certificate-{Guid.NewGuid():N}";
            var signedPropertiesId = $"Signature-SignedProperties-{Guid.NewGuid():N}";
            var objectId = $"Signature-Object-{Guid.NewGuid():N}";
            var referenceComprobanteId = $"Reference-comprobante-{Guid.NewGuid():N}";

            // Inicializar CustomSignedXml con soporte para resolución de IDs dentro de DataObject
            var signedXml = new CustomSignedXml(xmlDoc) { SigningKey = rsaKey };
            signedXml.Signature.Id = signatureId;
            signedXml.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;

            // 1. Referencia al documento comprobante (Enveloped) con Id para DataObjectFormat
            var referenceComprobante = new Reference { Uri = "#comprobante", Id = referenceComprobanteId };
            referenceComprobante.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            referenceComprobante.DigestMethod = SignedXml.XmlDsigSHA1Url;
            signedXml.AddReference(referenceComprobante);

            // 2. Referencia a SignedProperties (Obligatoria para XAdES-BES)
            var referenceSignedProps = new Reference { Uri = $"#{signedPropertiesId}" };
            referenceSignedProps.Type = "http://uri.etsi.org/01903#SignedProperties";
            referenceSignedProps.DigestMethod = SignedXml.XmlDsigSHA1Url;
            signedXml.AddReference(referenceSignedProps);

            // Construcción del nodo KeyInfo conteniendo el certificado X509 en base64
            var keyInfo = new KeyInfo { Id = keyInfoId };
            keyInfo.AddClause(new KeyInfoX509Data(certificado));

            // Inyección del módulo y exponente RSA
            var rsaKeyValue = new RSAKeyValue(rsaKey);
            keyInfo.AddClause(rsaKeyValue);
            signedXml.KeyInfo = keyInfo;

            // Inyección del nodo XAdES-BES (QualifyingProperties) versión 1.3.2
            var xadesObject = CrearNodoXadesBes(certificado, signatureId, signedPropertiesId, objectId, referenceComprobanteId);
            signedXml.AddObject(xadesObject);

            signedXml.ComputeSignature();
            var xmlFirma = signedXml.GetXml();

            xmlDoc.DocumentElement!.AppendChild(xmlDoc.ImportNode(xmlFirma, true));

            return xmlDoc;
        }

        private DataObject CrearNodoXadesBes(
            X509Certificate2 cert,
            string signatureId,
            string signedPropertiesId,
            string objectId,
            string referenceComprobanteId)
        {
            // Fecha y hora oficial en zona horaria de Ecuador (UTC-5)
            var ecuadorOffset = TimeSpan.FromHours(-5);
            var signingTime = new DateTimeOffset(DateTime.UtcNow).ToOffset(ecuadorOffset).ToString("yyyy-MM-ddTHH:mm:sszzz");

            var certHash = cert.GetCertHash(HashAlgorithmName.SHA1);
            var certDigestBase64 = Convert.ToBase64String(certHash);

            // Convertir número de serie hexadecimal a representación decimal exacta
            var serialNumberDecimal = BigInteger.Parse("0" + cert.SerialNumber, NumberStyles.HexNumber).ToString();

            string xadesNamespace = "http://uri.etsi.org/01903/v1.3.2#";

            var xadesXml = $@"
            <etsi:QualifyingProperties Target=""#{signatureId}"" xmlns=""http://www.w3.org/2000/09/xmldsig#"" xmlns:etsi=""{xadesNamespace}"">
                <etsi:SignedProperties Id=""{signedPropertiesId}"">
                    <etsi:SignedSignatureProperties>
                        <etsi:SigningTime>{signingTime}</etsi:SigningTime>
                        <etsi:SigningCertificate>
                            <etsi:Cert>
                                <etsi:CertDigest>
                                    <DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1"" />
                                    <DigestValue>{certDigestBase64}</DigestValue>
                                </etsi:CertDigest>
                                <etsi:IssuerSerial>
                                    <X509IssuerName>{cert.Issuer}</X509IssuerName>
                                    <X509SerialNumber>{serialNumberDecimal}</X509SerialNumber>
                                </etsi:IssuerSerial>
                            </etsi:Cert>
                        </etsi:SigningCertificate>
                    </etsi:SignedSignatureProperties>
                    <etsi:SignedDataObjectProperties>
                        <etsi:DataObjectFormat ObjectReference=""#{referenceComprobanteId}"">
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
                Id = objectId,
                Data = doc.DocumentElement?.SelectNodes(".")!
            };
        }

        private class CustomSignedXml : SignedXml
        {
            private readonly List<DataObject> _customDataObjects = new();

            public CustomSignedXml(XmlDocument document) : base(document) { }

            public new void AddObject(DataObject dataObject)
            {
                base.AddObject(dataObject);
                _customDataObjects.Add(dataObject);
            }

            public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
            {
                if (string.IsNullOrEmpty(idValue)) return null;

                var idElem = base.GetIdElement(document, idValue);
                if (idElem != null) return idElem;

                if (KeyInfo != null && KeyInfo.Id == idValue)
                {
                    return KeyInfo.GetXml();
                }

                if (document != null)
                {
                    var node = document.SelectSingleNode($"//*[@Id='{idValue}' or @id='{idValue}']");
                    if (node is XmlElement el) return el;
                }

                foreach (var dataObject in _customDataObjects)
                {
                    if (dataObject.Data != null)
                    {
                        foreach (XmlNode node in dataObject.Data)
                        {
                            if (node is XmlElement el && (el.GetAttribute("Id") == idValue || el.GetAttribute("id") == idValue))
                                return el;

                            var subNode = node.SelectSingleNode($"//*[@Id='{idValue}' or @id='{idValue}']");
                            if (subNode is XmlElement subEl)
                                return subEl;
                        }
                    }
                }

                return null;
            }
        }
    }
}
