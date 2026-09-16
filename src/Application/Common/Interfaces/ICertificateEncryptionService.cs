namespace BillingSaaS.Application.Common.Interfaces;

/// <summary>
/// Servicio criptográfico para cifrado autenticado en reposo de certificados digitales y credenciales sensibles (AES-256-GCM / NIST SP 800-38D).
/// Cumple con LOPDP, Ley de Comercio Electrónico del Ecuador y estándares OWASP.
/// </summary>
public interface ICertificateEncryptionService
{
    /// <summary>
    /// Cifra un arreglo de bytes utilizando AES-256-GCM con datos asociados autenticados opcionales (AAD).
    /// </summary>
    /// <param name="plainBytes">Datos en claro a cifrar.</param>
    /// <param name="associatedData">Datos asociados autenticados (ej. TenantId) para vincular criptográficamente el cifrado al tenant.</param>
    /// <returns>Arreglo empaquetado: [12 bytes Nonce] + [16 bytes Tag] + [N bytes Ciphertext].</returns>
    byte[] Encrypt(byte[] plainBytes, byte[]? associatedData = null);

    /// <summary>
    /// Descifra un arreglo empaquetado verificando autenticidad e integridad con AES-256-GCM.
    /// </summary>
    /// <param name="cipherPayload">Arreglo empaquetado que contiene Nonce, Tag y Ciphertext.</param>
    /// <param name="associatedData">Datos asociados autenticados (ej. TenantId) que deben coincidir con los usados al cifrar.</param>
    /// <returns>Datos originales en claro.</returns>
    byte[] Decrypt(byte[] cipherPayload, byte[]? associatedData = null);

    /// <summary>
    /// Cifra una cadena de texto (ej. contraseña del .p12) y devuelve una representación segura en Base64.
    /// </summary>
    string EncryptString(string plainText, byte[]? associatedData = null);

    /// <summary>
    /// Descifra una cadena en Base64 previamente cifrada con <see cref="EncryptString"/>.
    /// </summary>
    string DecryptString(string cipherBase64, byte[]? associatedData = null);
}

