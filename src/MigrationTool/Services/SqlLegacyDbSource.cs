using System.Data;
using BillingSaaS.Domain.Entities;
using BillingSaaS.MigrationTool.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillingSaaS.MigrationTool.Services;

public class SqlLegacyDbSource : IClienteMigracionSource
{
    private readonly string? _connectionString;
    private readonly MigrationOptions _options;
    private readonly IXmlFacturaParser _xmlParser;
    private readonly IPartnerCsvFilterService _csvFilter;
    private readonly ILogger<SqlLegacyDbSource> _logger;

    public SqlLegacyDbSource(
        IConfiguration configuration,
        IOptions<MigrationOptions> options,
        IXmlFacturaParser xmlParser,
        IPartnerCsvFilterService csvFilter,
        ILogger<SqlLegacyDbSource> logger)
    {
        _options = options.Value;
        _connectionString = !string.IsNullOrWhiteSpace(_options.LegacyConnectionString)
            ? _options.LegacyConnectionString
            : configuration.GetConnectionString("LegacyDb");
        _xmlParser = xmlParser;
        _csvFilter = csvFilter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClienteMigracionDto>> ObtenerClientesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogError("No se configuró la cadena de conexión 'LegacyDb' en appsettings.json.");
            return Array.Empty<ClienteMigracionDto>();
        }

        var clientes = new List<ClienteMigracionDto>();

        try
        {
            // Cargar archivo CSV de filtrado si fue provisto
            _csvFilter.CargarCsv(_options.FilterCsvPath);

            _logger.LogInformation("Conectando a la base de datos de origen (LegacyDb)...");
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            _logger.LogInformation("Conexión establecida con éxito con la base de datos antigua.");

            var legacyAppTables = new List<string>();
            using (var cmdTbls = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME LIKE 'App%';", connection))
            {
                using var rTbls = await cmdTbls.ExecuteReaderAsync(cancellationToken);
                while (await rTbls.ReadAsync(cancellationToken))
                {
                    legacyAppTables.Add(rTbls.GetString(0));
                }
            }
            _logger.LogInformation("Tablas de aplicación detectadas en LegacyDb: {Tables}", string.Join(", ", legacyAppTables));

            // 0. Detectar columna de logotipo en AppUsersInfo dinámicamente
            var appUsersInfoColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdColsInfo = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppUsersInfo';", connection))
            {
                using var readerColsInfo = await cmdColsInfo.ExecuteReaderAsync(cancellationToken);
                while (await readerColsInfo.ReadAsync(cancellationToken))
                {
                    appUsersInfoColumns[readerColsInfo.GetString(0)] = readerColsInfo.GetString(1);
                }
            }

            string? logoColName = null;
            foreach (var candidate in new[] { "Logo", "LogoBytes", "LogoBase64", "LogoData", "Image", "LogoFile", "Photo", "Avatar", "FileBytes", "LogoImage" })
            {
                if (appUsersInfoColumns.ContainsKey(candidate))
                {
                    logoColName = candidate;
                    break;
                }
            }

            if (logoColName != null)
            {
                _logger.LogInformation("Se detectó columna de logotipo '{Col}' en AppUsersInfo.", logoColName);
            }

            string logoSelectClause = logoColName != null ? $"ui.[{logoColName}] AS UserLogo," : "NULL AS UserLogo,";

            // 0.1 Detectar columnas de AppSigns dinámicamente
            var appSignsColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdColsSigns = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppSigns';", connection))
            {
                using var readerColsSigns = await cmdColsSigns.ExecuteReaderAsync(cancellationToken);
                while (await readerColsSigns.ReadAsync(cancellationToken))
                {
                    appSignsColumns[readerColsSigns.GetString(0)] = readerColsSigns.GetString(1);
                }
            }

            string? signFileCol = null;
            foreach (var candidate in new[] { "FileBytes", "File", "SignFile", "SignBytes", "Certificate", "Certificado", "Base64", "FileBase64", "Content", "Document" })
            {
                if (appSignsColumns.ContainsKey(candidate))
                {
                    signFileCol = candidate;
                    break;
                }
            }

            string? signPasswordCol = null;
            foreach (var candidate in new[] { "Password", "SignPassword", "Clave", "PasswordFile", "Pin", "ClaveFirma" })
            {
                if (appSignsColumns.ContainsKey(candidate))
                {
                    signPasswordCol = candidate;
                    break;
                }
            }

            string signJoinCol = appSignsColumns.ContainsKey("CreatorId") ? "CreatorId" : (appSignsColumns.ContainsKey("UserId") ? "UserId" : "CreatorId");
            string signPasswordSelect = signPasswordCol != null ? $"s.[{signPasswordCol}] AS SignPassword," : "NULL AS SignPassword,";
            string signFileSelect = signFileCol != null ? $"s.[{signFileCol}] AS SignFileBytes" : "NULL AS SignFileBytes";

            // 1. Consultar usuarios y su información tributaria/perfil
            string queryUsuarios = $@"
                SELECT 
                    u.Id AS UserId,
                    u.UserName,
                    u.Email,
                    ISNULL(ui.RazonSocial, u.UserName) AS RazonSocial,
                    ui.NombreComercial,
                    ISNULL(ui.Ruc, '') AS Ruc,
                    ui.PlanName,
                    ui.LastSequenceProd,
                    {logoSelectClause}
                    {signPasswordSelect}
                    {signFileSelect}
                FROM AbpUsers u
                LEFT JOIN AppUsersInfo ui ON ui.CreatorId = u.Id
                LEFT JOIN AppSigns s ON s.[{signJoinCol}] = u.Id
                WHERE u.IsDeleted = 0
                ORDER BY u.CreationTime DESC;
            ";

            var usuariosMap = new Dictionary<Guid, ClienteMigracionDto>();
            int totalUsuariosEncontrados = 0;

            using (var cmd = new SqlCommand(queryUsuarios, connection))
            {
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    totalUsuariosEncontrados++;
                    var userId = reader.GetGuid(reader.GetOrdinal("UserId"));
                    var userName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? string.Empty : reader.GetString(reader.GetOrdinal("UserName"));
                    var email = reader.IsDBNull(reader.GetOrdinal("Email")) ? userName : reader.GetString(reader.GetOrdinal("Email"));
                    var razonSocial = reader.IsDBNull(reader.GetOrdinal("RazonSocial")) ? userName : reader.GetString(reader.GetOrdinal("RazonSocial"));
                    var nombreComercial = reader.IsDBNull(reader.GetOrdinal("NombreComercial")) ? null : reader.GetString(reader.GetOrdinal("NombreComercial"));
                    var ruc = reader.IsDBNull(reader.GetOrdinal("Ruc")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ruc"));
                    var lastSeq = reader.IsDBNull(reader.GetOrdinal("LastSequenceProd")) ? 0 : (int)reader.GetInt64(reader.GetOrdinal("LastSequenceProd"));

                    // Filtrar por CSV si el filtro está activo
                    PartnerCsvEntry? csvEntry = null;
                    if (_csvFilter.IsFilterActive)
                    {
                        if (!_csvFilter.TryGetEntry(userName, email, ruc, out csvEntry))
                        {
                            // Usuario no presente en el CSV -> Omitir de la migración
                            continue;
                        }
                    }

                    // Firma digital: leer contraseña en texto plano directamente desde la base de datos antigua
                    var signPasswordDb = !reader.IsDBNull(reader.GetOrdinal("SignPassword"))
                        ? reader.GetString(reader.GetOrdinal("SignPassword"))?.Trim() ?? string.Empty
                        : string.Empty;

                    var signPasswordFinal = !string.IsNullOrWhiteSpace(signPasswordDb)
                        ? signPasswordDb
                        : (csvEntry?.PasswordFirma ?? string.Empty);

                    // Archivo .p12: puede ser Base64 directo o varbinary (bytes)
                    string certBase64 = string.Empty;
                    if (!reader.IsDBNull(reader.GetOrdinal("SignFileBytes")))
                    {
                        var val = reader["SignFileBytes"];
                        if (val is byte[] rawCert && rawCert.Length > 0)
                        {
                            certBase64 = Convert.ToBase64String(rawCert);
                        }
                        else if (val is string certStr && !string.IsNullOrWhiteSpace(certStr))
                        {
                            certBase64 = certStr.Trim();
                        }
                    }

                    // Logotipo del negocio (normalizado a Data URI)
                    string? logoBase64 = null;
                    if (logoColName != null && !reader.IsDBNull(reader.GetOrdinal("UserLogo")))
                    {
                        var colType = appUsersInfoColumns[logoColName].ToLowerInvariant();
                        if (colType.Contains("binary") || colType.Contains("image"))
                        {
                            var logoBytes = (byte[])reader["UserLogo"];
                            if (logoBytes.Length > 0)
                            {
                                logoBase64 = Convert.ToBase64String(logoBytes);
                            }
                        }
                        else
                        {
                            var rawLogoStr = reader.GetString(reader.GetOrdinal("UserLogo"))?.Trim();
                            if (!string.IsNullOrWhiteSpace(rawLogoStr))
                            {
                                logoBase64 = rawLogoStr;
                            }
                        }
                    }

                    logoBase64 = Emisor.NormalizarLogo(logoBase64);

                    var passwordUsuario = !string.IsNullOrWhiteSpace(csvEntry?.PasswordUsuario)
                        ? csvEntry.PasswordUsuario
                        : "Temporal123*";

                    var clienteDto = new ClienteMigracionDto
                    {
                        PartnerId = _options.PartnerId,
                        NombreOrganizacion = !string.IsNullOrWhiteSpace(razonSocial) ? razonSocial : userName,
                        Username = userName,
                        Email = email,
                        PasswordPlana = passwordUsuario,
                        PlanCodigo = _options.PlanCodigoDefault,
                        FechaInicioPlan = DateTime.UtcNow.AddMonths(-1),
                        FechaFinPlan = DateTime.UtcNow.AddMonths(11), // 1 año de vigencia
                        Frecuencia = "ANUAL",
                        DiasGracia = 3,
                        CertificadoBase64 = certBase64,
                        PasswordCertificado = signPasswordFinal,
                        Ruc = ruc,
                        RazonSocial = razonSocial,
                        NombreComercial = nombreComercial,
                        UltimoSecuencialFactura = lastSeq,
                        Logo = logoBase64
                    };

                    usuariosMap[userId] = clienteDto;
                    clientes.Add(clienteDto);
                }
            }

            if (_csvFilter.IsFilterActive)
            {
                _logger.LogInformation("Base de datos antigua tiene {Total} usuarios. Filtrados para migración según CSV: {Filtrados} usuarios.", totalUsuariosEncontrados, clientes.Count);
            }
            else
            {
                _logger.LogInformation("Se encontraron {Count} usuarios en la base de datos antigua.", clientes.Count);
            }

            // 2. Extraer documentos / facturas emitidas por cada usuario
            var appDocsColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdCols = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AppDocuments';", connection))
            {
                using var readerCols = await cmdCols.ExecuteReaderAsync(cancellationToken);
                while (await readerCols.ReadAsync(cancellationToken))
                {
                    appDocsColumns.Add(readerCols.GetString(0));
                }
            }

            string isDeletedFilter = appDocsColumns.Contains("IsDeleted") ? "d.IsDeleted = 0 AND " : "";
            string stateCol = appDocsColumns.Contains("State") ? "d.State" : "0 AS State";
            string creationTimeCol = appDocsColumns.Contains("CreationTime") ? "d.CreationTime" : "GETUTCDATE() AS CreationTime";
            string orderByClause = appDocsColumns.Contains("CreationTime") ? "ORDER BY d.CreationTime ASC" : "";

            var queryDocumentos = $@"
                SELECT 
                    d.CreatorId,
                    {stateCol},
                    d.XmlValue,
                    {creationTimeCol}
                FROM AppDocuments d
                WHERE {isDeletedFilter}d.XmlValue IS NOT NULL AND LEN(d.XmlValue) > 0
                {orderByClause};
            ";

            using (var cmdDocs = new SqlCommand(queryDocumentos, connection))
            {
                cmdDocs.CommandTimeout = 180;
                using var readerDocs = await cmdDocs.ExecuteReaderAsync(cancellationToken);
                int docsLeidos = 0;

                while (await readerDocs.ReadAsync(cancellationToken))
                {
                    if (readerDocs.IsDBNull(readerDocs.GetOrdinal("CreatorId"))) continue;
                    var creatorId = readerDocs.GetGuid(readerDocs.GetOrdinal("CreatorId"));

                    if (!usuariosMap.TryGetValue(creatorId, out var clienteDto)) continue;

                    var xml = readerDocs.GetString(readerDocs.GetOrdinal("XmlValue"));
                    var stateInt = readerDocs.IsDBNull(readerDocs.GetOrdinal("State")) ? 0 : readerDocs.GetInt32(readerDocs.GetOrdinal("State"));
                    var creationTime = readerDocs.IsDBNull(readerDocs.GetOrdinal("CreationTime")) ? DateTime.UtcNow : readerDocs.GetDateTime(readerDocs.GetOrdinal("CreationTime"));

                    // Mapear estado ABP enum (0: NoEnviado, 1: Enviado, 2: Autorizado, 3: Rechazado, etc.)
                    var estadoStr = stateInt switch
                    {
                        2 => "AUTORIZADO",
                        3 => "DEVUELTA",
                        4 => "NO AUTORIZADO",
                        1 => "RECIBIDA",
                        _ => "AUTORIZADO"
                    };

                    try
                    {
                        var facturaDto = _xmlParser.ParsearFacturaXml(xml, estadoStr);
                        if (facturaDto.FechaEmision == default)
                        {
                            facturaDto.FechaEmision = creationTime;
                        }
                        clienteDto.Facturas.Add(facturaDto);
                        docsLeidos++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error al parsear documento XML de usuario {UserId}", creatorId);
                    }
                }

                _logger.LogInformation("Se leyeron y parsearon {DocsCount} comprobantes XML asociados a los clientes.", docsLeidos);
            }

            // 3. Enriquecer datos tributarios de cada cliente a partir de sus XMLs
            foreach (var cliente in clientes)
            {
                _xmlParser.EnriquecerEmisorDesdeFacturas(cliente);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al extraer datos de la base de datos antigua.");
            throw;
        }

        return clientes;
    }
}
