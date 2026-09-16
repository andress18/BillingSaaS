using System.Security.Cryptography;
using System.Text;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.Infrastructure.Security;

/// <summary>
/// Implementación de cifrado autenticado en reposo con AES-256-GCM (NIST SP 800-38D).
/// Cumple con la Ley Orgánica de Protección de Datos Personales (LOPDP), Ley de Comercio Electrónico
/// y recomendaciones OWASP / NIST para custodia segura de certificados PKCS#12 y contraseñas.
/// </summary>
public class AesGcmCertificateEncryptionService : ICertificateEncryptionService
{
    private const int NonceSize = 12; // 96 bits recomendado por NIST SP 800-38D
    private const int TagSize = 16;   // 128 bits para autenticación e integridad total
    private const int KeySize = 32;   // 256 bits para AES-256

    private readonly byte[] _masterKey;
    private readonly ILogger<AesGcmCertificateEncryptionService> _logger;

    public AesGcmCertificateEncryptionService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AesGcmCertificateEncryptionService> logger)
    {
        _logger = logger;
        _masterKey = ResolveMasterKey(configuration, environment);
    }

    /// <summary>
    /// Constructor alternativo para pruebas unitarias con llave provista directamente.
    /// </summary>
    public AesGcmCertificateEncryptionService(byte[] key, ILogger<AesGcmCertificateEncryptionService> logger)
    {
        if (key == null || key.Length != KeySize)
            throw new ArgumentException($"La llave de cifrado debe tener exactamente {KeySize} bytes (256 bits).", nameof(key));

        _masterKey = (byte[])key.Clone();
        _logger = logger;
    }

    private byte[] ResolveMasterKey(IConfiguration configuration, IHostEnvironment environment)
    {
        // 1. Intentar variable de entorno del sistema o contenedor
        var envKey = Environment.GetEnvironmentVariable("BILLING_CERTIFICATE_ENCRYPTION_KEY");

        // 2. Intentar configuración de aplicación o Secret Manager
        var configKey = configuration["Security:CertificateEncryptionKey"];

        var rawKey = !string.IsNullOrWhiteSpace(envKey) ? envKey : configKey;

        if (!string.IsNullOrWhiteSpace(rawKey))
        {
            try
            {
                var decoded = rawKey.Length == 64 && rawKey.All(Uri.IsHexDigit)
                    ? Convert.FromHexString(rawKey)
                    : Convert.FromBase64String(rawKey);

                if (decoded.Length != KeySize)
                {
                    throw new InvalidOperationException(
                        $"La clave de cifrado configurada no tiene una longitud válida de 256 bits ({KeySize} bytes). Longitud detectada: {decoded.Length} bytes.");
                }

                return decoded;
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("La clave 'Security:CertificateEncryptionKey' debe estar codificada en Base64 o Hexadecimal de 256 bits.", ex);
            }
        }

        // 3. Fallback controlado únicamente para desarrollo local
        if (environment.IsDevelopment())
        {
            _logger.LogWarning("SEGURIDAD: 'BILLING_CERTIFICATE_ENCRYPTION_KEY' no está configurada. Utilizando llave predeterminada de desarrollo. Para producción, DEBE configurar un gestor de secretos o variable de entorno.");
            return SHA256.HashData(Encoding.UTF8.GetBytes("BillingSaaS_Dev_MasterKey_2026_LocalOnly_DeterministicKey"));
        }

        throw new InvalidOperationException(
            "CRÍTICO: No se ha configurado la llave maestra de cifrado 'BILLING_CERTIFICATE_ENCRYPTION_KEY' o 'Security:CertificateEncryptionKey'. " +
            "El sistema no puede iniciar en modo no-desarrollo sin cifrado en reposo para dar cumplimiento a la LOPDP.");
    }

    public byte[] Encrypt(byte[] plainBytes, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);

        if (plainBytes.Length == 0)
            return [];

        // Generar Nonce aleatorio único de 12 bytes
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var ciphertext = new byte[plainBytes.Length];

        using (var aesGcm = new AesGcm(_masterKey, TagSize))
        {
            aesGcm.Encrypt(
                nonce: nonce,
                plaintext: plainBytes,
                ciphertext: ciphertext,
                tag: tag,
                associatedData: associatedData);
        }

        // Estructura de salida: [12B Nonce][16B Tag][N Bytes Ciphertext]
        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);

        return payload;
    }

    public byte[] Decrypt(byte[] cipherPayload, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(cipherPayload);

        if (cipherPayload.Length == 0)
            return [];

        const int minSize = NonceSize + TagSize;
        if (cipherPayload.Length < minSize)
        {
            throw new CryptographicException(
                "El paquete de datos cifrados está corrupto o no tiene el formato autenticado esperado.");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertextLength = cipherPayload.Length - minSize;
        var ciphertext = new byte[ciphertextLength];
        var plaintext = new byte[ciphertextLength];

        Buffer.BlockCopy(cipherPayload, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(cipherPayload, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(cipherPayload, NonceSize + TagSize, ciphertext, 0, ciphertextLength);

        using (var aesGcm = new AesGcm(_masterKey, TagSize))
        {
            aesGcm.Decrypt(
                nonce: nonce,
                ciphertext: ciphertext,
                tag: tag,
                plaintext: plaintext,
                associatedData: associatedData);
        }

        return plaintext;
    }

    public string EncryptString(string plainText, byte[]? associatedData = null)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = Encrypt(bytes, associatedData);

        return Convert.ToBase64String(encrypted);
    }

    public string DecryptString(string cipherBase64, byte[]? associatedData = null)
    {
        if (string.IsNullOrEmpty(cipherBase64))
            return string.Empty;

        var cipherBytes = Convert.FromBase64String(cipherBase64);
        var decrypted = Decrypt(cipherBytes, associatedData);

        return Encoding.UTF8.GetString(decrypted);
    }
}

