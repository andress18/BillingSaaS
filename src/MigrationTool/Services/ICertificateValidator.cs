namespace BillingSaaS.MigrationTool.Services;

public record CertificateValidationResult(
    bool EsValido,
    byte[] CertificadoBytes,
    string PasswordPlana,
    DateTime? FechaCaducidad,
    string? Subject,
    bool EstaCaducado,
    string? ErrorMensaje);

public interface ICertificateValidator
{
    CertificateValidationResult ValidarCertificado(string? certificadoBase64, string? password);
}
