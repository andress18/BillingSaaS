namespace BillingSaaS.Domain.Entities;

public class Emisor : BaseAuditableEntity
{
    // Aislamiento Multi-tenant (Partner / Socio)
    public Guid TenantId { get; private set; }

    // Datos Tributarios del Emisor
    public string Ruc { get; private set; } = null!;
    public string RazonSocial { get; private set; } = null!;
    public string? NombreComercial { get; private set; }
    public string DireccionMatriz { get; private set; } = null!;
    public string? DireccionEstablecimiento { get; private set; }

    // Punto de Venta / Emisión
    public string CodigoEstablecimiento { get; private set; } = null!; // Ej: "001"
    public string PuntoEmision { get; private set; } = null!;          // Ej: "001"
    public int SecuencialFactura { get; private set; }                 // Contador atómico (1, 2, 3...)
    public int SecuencialNotaDebito { get; private set; }              // Contador atómico para Notas de Débito (1, 2, 3...)
    public int SecuencialNotaCredito { get; private set; }             // Contador atómico para Notas de Crédito (1, 2, 3...)

    // Configuración SRI
    public int Ambiente { get; private set; }                          // 1: Pruebas, 2: Producción
    public bool ObligadoContabilidad { get; private set; }
    public string? RegimenRimpe { get; private set; }                  // Ej: "CONTRIBUYENTE RÉGIMEN RIMPE"
    public string? ContribuyenteEspecial { get; private set; }         // Número de resolución si aplica
    public string? Logo { get; private set; }                          // Logotipo del negocio en formato Base64 / Data URI

    // Firma Electrónica (.p12 encriptado)
    public byte[]? CertificadoDigital { get; private set; }
    public string? PasswordCertificado { get; private set; }
    public DateTime? FechaCaducidadCertificado { get; private set; }
    public string? SubjectCertificado { get; private set; }

    public bool Activo { get; private set; } = true;

    private Emisor() { }

    public static Emisor Crear(
        Guid tenantId,
        string ruc,
        string razonSocial,
        string direccionMatriz,
        string codigoEstablecimiento = "001",
        string puntoEmision = "001",
        int ambiente = 1,
        bool obligadoContabilidad = false,
        string? nombreComercial = null,
        string? direccionEstablecimiento = null,
        string? regimenRimpe = null,
        string? contribuyenteEspecial = null,
        int secuencialInicial = 0,
        string? logo = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 13 || !ruc.All(char.IsDigit))
            throw new ArgumentException("El RUC del emisor debe tener exactamente 13 dígitos numéricos.", nameof(ruc));

        if (string.IsNullOrWhiteSpace(razonSocial) || razonSocial.Length > 300)
            throw new ArgumentException("La razón social es obligatoria y no puede exceder 300 caracteres.", nameof(razonSocial));

        if (string.IsNullOrWhiteSpace(direccionMatriz) || direccionMatriz.Length > 300)
            throw new ArgumentException("La dirección matriz es obligatoria y no puede exceder 300 caracteres.", nameof(direccionMatriz));

        return string.IsNullOrWhiteSpace(codigoEstablecimiento) || codigoEstablecimiento.Length != 3 || !codigoEstablecimiento.All(char.IsDigit)
            ? throw new ArgumentException("El código de establecimiento debe tener exactamente 3 dígitos numéricos.", nameof(codigoEstablecimiento))
            : string.IsNullOrWhiteSpace(puntoEmision) || puntoEmision.Length != 3 || !puntoEmision.All(char.IsDigit)
            ? throw new ArgumentException("El punto de emisión debe tener exactamente 3 dígitos numéricos.", nameof(puntoEmision))
            : ambiente is not (1 or 2)
            ? throw new ArgumentException("El ambiente debe ser 1 (Pruebas) o 2 (Producción).", nameof(ambiente))
            : new Emisor
        {
            TenantId = tenantId,
            Ruc = ruc,
            RazonSocial = razonSocial,
            NombreComercial = nombreComercial,
            DireccionMatriz = direccionMatriz,
            DireccionEstablecimiento = direccionEstablecimiento,
            CodigoEstablecimiento = codigoEstablecimiento,
            PuntoEmision = puntoEmision,
            SecuencialFactura = secuencialInicial > 0 ? secuencialInicial - 1 : 0,
            Ambiente = ambiente,
            ObligadoContabilidad = obligadoContabilidad,
            RegimenRimpe = Constants.RegimenRimpeTipos.Normalizar(regimenRimpe),
            ContribuyenteEspecial = contribuyenteEspecial,
            Logo = logo,
            Activo = true
        };
    }

    public void ActualizarLogo(string? logo)
    {
        Logo = logo;
    }

    public void ConfigurarCertificado(
        byte[] certificadoBytes,
        string passwordCifrado,
        DateTime fechaCaducidad,
        string subject)
    {
        if (certificadoBytes == null || certificadoBytes.Length == 0)
            throw new ArgumentException("El archivo del certificado no puede estar vacío.", nameof(certificadoBytes));

        if (string.IsNullOrWhiteSpace(passwordCifrado))
            throw new ArgumentException("La contraseña del certificado es obligatoria.", nameof(passwordCifrado));

        CertificadoDigital = certificadoBytes;
        PasswordCertificado = passwordCifrado;
        FechaCaducidadCertificado = fechaCaducidad;
        SubjectCertificado = subject;
    }

    public void EliminarCertificado()
    {
        CertificadoDigital = null;
        PasswordCertificado = null;
        FechaCaducidadCertificado = null;
        SubjectCertificado = null;
    }

    public bool TieneCertificadoValido()
    {
        return CertificadoDigital != null &&
               CertificadoDigital.Length > 0 &&
               !string.IsNullOrWhiteSpace(PasswordCertificado) &&
               FechaCaducidadCertificado.HasValue &&
               FechaCaducidadCertificado.Value > DateTime.UtcNow;
    }

    public string ObtenerSiguienteSecuencialFactura()
    {
        SecuencialFactura++;
        return SecuencialFactura.ToString("D9");
    }

    public string ObtenerSiguienteSecuencialNotaDebito()
    {
        SecuencialNotaDebito++;
        return SecuencialNotaDebito.ToString("D9");
    }

    public void EstablecerSecuencialNotaDebito(int secuencial)
    {
        if (secuencial < 0)
            throw new ArgumentException("El secuencial debe ser mayor o igual a cero.", nameof(secuencial));
        SecuencialNotaDebito = secuencial > 0 ? secuencial - 1 : 0;
    }

    public string ObtenerSiguienteSecuencialNotaCredito()
    {
        SecuencialNotaCredito++;
        return SecuencialNotaCredito.ToString("D9");
    }

    public void EstablecerSecuencialNotaCredito(int secuencial)
    {
        if (secuencial < 0)
            throw new ArgumentException("El secuencial debe ser mayor o igual a cero.", nameof(secuencial));
        SecuencialNotaCredito = secuencial > 0 ? secuencial - 1 : 0;
    }

    public void ActualizarAmbiente(int nuevoAmbiente)
    {
        if (nuevoAmbiente is not (1 or 2))
            throw new ArgumentException("El ambiente debe ser 1 (Pruebas) o 2 (Producción).", nameof(nuevoAmbiente));

        Ambiente = nuevoAmbiente;
    }

    public void ActualizarDatosTributarios(
        string ruc,
        string razonSocial,
        string direccionMatriz,
        string? nombreComercial = null,
        string? direccionEstablecimiento = null,
        string codigoEstablecimiento = "001",
        string puntoEmision = "001",
        int ambiente = 1,
        bool obligadoContabilidad = false,
        string? regimenRimpe = null,
        string? contribuyenteEspecial = null,
        int secuencialInicial = 0)
    {
        if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 13 || !ruc.All(char.IsDigit))
            throw new ArgumentException("El RUC del emisor debe tener exactamente 13 dígitos numéricos.", nameof(ruc));
        
        if (string.IsNullOrWhiteSpace(razonSocial) || razonSocial.Length > 300)
            throw new ArgumentException("La razón social es obligatoria y no puede exceder 300 caracteres.", nameof(razonSocial));

        if (string.IsNullOrWhiteSpace(direccionMatriz) || direccionMatriz.Length > 300)
            throw new ArgumentException("La dirección matriz es obligatoria y no puede exceder 300 caracteres.", nameof(direccionMatriz));

        if (string.IsNullOrWhiteSpace(codigoEstablecimiento) || codigoEstablecimiento.Length != 3 || !codigoEstablecimiento.All(char.IsDigit))
            throw new ArgumentException("El código de establecimiento debe tener exactamente 3 dígitos numéricos.", nameof(codigoEstablecimiento));

        if (string.IsNullOrWhiteSpace(puntoEmision) || puntoEmision.Length != 3 || !puntoEmision.All(char.IsDigit))
            throw new ArgumentException("El punto de emisión debe tener exactamente 3 dígitos numéricos.", nameof(puntoEmision));

        if (ambiente is not (1 or 2))
            throw new ArgumentException("El ambiente debe ser 1 (Pruebas) o 2 (Producción).", nameof(ambiente));

        if (secuencialInicial < 0)
            throw new ArgumentException("El secuencial inicial debe ser mayor a cero.", nameof(secuencialInicial));

        if (secuencialInicial > 0)
        {
            SecuencialFactura = secuencialInicial - 1;
        }

        Ruc = ruc;
        RazonSocial = razonSocial;
        DireccionMatriz = direccionMatriz;
        NombreComercial = nombreComercial;
        DireccionEstablecimiento = direccionEstablecimiento;
        CodigoEstablecimiento = codigoEstablecimiento;
        PuntoEmision = puntoEmision;
        Ambiente = ambiente;
        ObligadoContabilidad = obligadoContabilidad;
        RegimenRimpe = Constants.RegimenRimpeTipos.Normalizar(regimenRimpe);
        ContribuyenteEspecial = contribuyenteEspecial;
    }

    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;
}

