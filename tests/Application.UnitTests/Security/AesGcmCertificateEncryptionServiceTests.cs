using System.Security.Cryptography;
using System.Text;
using BillingSaaS.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Security;

[TestFixture]
public class AesGcmCertificateEncryptionServiceTests
{
    private AesGcmCertificateEncryptionService _service = null!;
    private byte[] _testKey = null!;

    [SetUp]
    public void SetUp()
    {
        _testKey = SHA256.HashData(Encoding.UTF8.GetBytes("UnitTest_SecretKey_2026_TestOnly!"));
        _service = new AesGcmCertificateEncryptionService(_testKey, NullLogger<AesGcmCertificateEncryptionService>.Instance);
    }

    [Test]
    public void Encrypt_Decrypt_Bytes_DebeRetornarDatosOriginales()
    {
        var rawData = Encoding.UTF8.GetBytes("Certificado Digital .p12 de Prueba con Contenido Confidencial");
        var aad = Guid.NewGuid().ToByteArray();

        var encrypted = _service.Encrypt(rawData, aad);

        encrypted.ShouldNotBeNull();
        encrypted.Length.ShouldBeGreaterThan(rawData.Length);
        encrypted.ShouldNotBe(rawData);

        var decrypted = _service.Decrypt(encrypted, aad);

        decrypted.ShouldBe(rawData);
    }

    [Test]
    public void EncryptString_DecryptString_DebeRetornarTextoOriginal()
    {
        const string passwordOriginal = "MiContraseñaSuperSegura123!";
        var aad = Guid.NewGuid().ToByteArray();

        var encryptedBase64 = _service.EncryptString(passwordOriginal, aad);

        encryptedBase64.ShouldNotBeNullOrWhiteSpace();
        encryptedBase64.ShouldNotBe(passwordOriginal);

        var decrypted = _service.DecryptString(encryptedBase64, aad);

        decrypted.ShouldBe(passwordOriginal);
    }

    [Test]
    public void Decrypt_ConDiferenteAad_DebeLanzarCryptographicException_GarantizandoAislamientoMultiTenant()
    {
        var rawData = Encoding.UTF8.GetBytes("ContraseñaSecreta");
        var tenantA = Guid.NewGuid().ToByteArray();
        var tenantB = Guid.NewGuid().ToByteArray();

        var encrypted = _service.Encrypt(rawData, tenantA);

        // Intentar descifrar con el TenantId de otra empresa debe fallar inmediatamente
        Should.Throw<CryptographicException>(() => _service.Decrypt(encrypted, tenantB));
    }

    [Test]
    public void Decrypt_DatosAlterados_DebeLanzarCryptographicException_PorFallaDeTagAutenticado()
    {
        var rawData = Encoding.UTF8.GetBytes("DatosCriticosDeFirma");
        var aad = Guid.NewGuid().ToByteArray();

        var encrypted = _service.Encrypt(rawData, aad);

        // Alterar un byte del ciphertext para simular manipulación o corrupción
        encrypted[^1] ^= 0xFF;

        Should.Throw<CryptographicException>(() => _service.Decrypt(encrypted, aad));
    }

    [Test]
    public void Encrypt_MismoTexto_GeneraCifradosDistintosPorUsoDeNonceAleatorio()
    {
        const string password = "PasswordConstante123";
        var aad = Guid.NewGuid().ToByteArray();

        var encrypted1 = _service.EncryptString(password, aad);
        var encrypted2 = _service.EncryptString(password, aad);

        // OWASP: Cada cifrado debe usar un Nonce (IV) aleatorio único
        encrypted1.ShouldNotBe(encrypted2);

        // Ambos deben descifrarse al mismo valor original
        _service.DecryptString(encrypted1, aad).ShouldBe(password);
        _service.DecryptString(encrypted2, aad).ShouldBe(password);
    }

    [Test]
    public void Constructor_LlaveTamanoInvalido_DebeLanzarArgumentException()
    {
        var invalidKey = new byte[16]; // 128 bits en lugar de 256 bits

        Should.Throw<ArgumentException>(() => 
            new AesGcmCertificateEncryptionService(invalidKey, NullLogger<AesGcmCertificateEncryptionService>.Instance));
    }
}

