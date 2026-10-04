using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.MigrationTool.Services;

public class X509CertificateValidator : ICertificateValidator
{
    private readonly ILogger<X509CertificateValidator> _logger;

    public X509CertificateValidator(ILogger<X509CertificateValidator> logger)
    {
        _logger = logger;
    }

    public CertificateValidationResult ValidarCertificado(string? certificadoBase64, string? password)
    {
        if (string.IsNullOrWhiteSpace(certificadoBase64))
        {
            return new CertificateValidationResult(
                EsValido: false,
                CertificadoBytes: [],
                PasswordPlana: string.Empty,
                FechaCaducidad: null,
                Subject: null,
                EstaCaducado: false,
                ErrorMensaje: "No se proporcionó archivo de firma electrónica en Base64.");
        }

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(certificadoBase64.Trim());
        }
        catch (FormatException ex)
        {
            return new CertificateValidationResult(
                EsValido: false,
                CertificadoBytes: [],
                PasswordPlana: password ?? string.Empty,
                FechaCaducidad: null,
                Subject: null,
                EstaCaducado: false,
                ErrorMensaje: $"El certificado digital no contiene un Base64 válido: {ex.Message}");
        }

        if (rawBytes.Length == 0)
        {
            return new CertificateValidationResult(
                EsValido: false,
                CertificadoBytes: [],
                PasswordPlana: password ?? string.Empty,
                FechaCaducidad: null,
                Subject: null,
                EstaCaducado: false,
                ErrorMensaje: "El archivo del certificado está vacío (0 bytes).");
        }

        try
        {
            // En .NET 9 y .NET 10 se utiliza la API recomendada moderna X509CertificateLoader.LoadPkcs12
            using var cert = X509CertificateLoader.LoadPkcs12(rawBytes, password ?? string.Empty);
            
            var fechaCaducidadUtc = cert.NotAfter.ToUniversalTime();
            var subject = cert.Subject;
            var estaCaducado = fechaCaducidadUtc < DateTime.UtcNow;

            return new CertificateValidationResult(
                EsValido: true,
                CertificadoBytes: rawBytes,
                PasswordPlana: password ?? string.Empty,
                FechaCaducidad: fechaCaducidadUtc,
                Subject: subject,
                EstaCaducado: estaCaducado,
                ErrorMensaje: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cargar/validar certificado PKCS#12 con X509CertificateLoader.");
            return new CertificateValidationResult(
                EsValido: false,
                CertificadoBytes: rawBytes,
                PasswordPlana: password ?? string.Empty,
                FechaCaducidad: null,
                Subject: null,
                EstaCaducado: false,
                ErrorMensaje: $"No se pudo abrir el archivo .p12 con la clave proporcionada: {ex.Message}");
        }
    }
}
