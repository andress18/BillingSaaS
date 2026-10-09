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

                    DateTime fechaFinPlan = csvEntry?.FechaFinSuscripcion ?? DateTime.UtcNow.AddMonths(11);
                    DateTime fechaInicioPlan = fechaFinPlan.AddYears(-1);
                    if (fechaFinPlan < DateTime.UtcNow && fechaInicioPlan > fechaFinPlan)
                    {
                        fechaInicioPlan = fechaFinPlan.AddMonths(-1);
                    }

                    var clienteDto = new ClienteMigracionDto
                    {
                        PartnerId = _options.PartnerId,
                        NombreOrganizacion = !string.IsNullOrWhiteSpace(razonSocial) ? razonSocial : userName,
                        Username = userName,
                        Email = email,
                        PasswordPlana = passwordUsuario,
                        PlanCodigo = _options.PlanCodigoDefault,
                        FechaInicioPlan = fechaInicioPlan,
                        FechaFinPlan = fechaFinPlan,
                        TieneFechaFinPlanExplicita = csvEntry?.FechaFinSuscripcion.HasValue ?? false,
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

            // 3. Extraer catálogo de compradores directos (clientes que tal vez nunca tuvieron facturas)
            await ExtraerCompradoresDirectosAsync(connection, legacyAppTables, usuariosMap, cancellationToken);

            // 4. Extraer catálogo de productos y servicios directos (items que tal vez nunca tuvieron facturas)
            await ExtraerProductosDirectosAsync(connection, legacyAppTables, usuariosMap, cancellationToken);

            // 5. Enriquecer datos tributarios de cada cliente a partir de sus XMLs
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

    private async Task ExtraerCompradoresDirectosAsync(
        SqlConnection connection,
        List<string> legacyAppTables,
        Dictionary<Guid, ClienteMigracionDto> usuariosMap,
        CancellationToken cancellationToken)
    {
        var clientTableCandidates = new[]
        {
            "AppClients", "AppCustomers", "AppClientes", "AppCompradores", "AppReceptors",
            "AppReceptores", "AppBuyers", "AppClient", "AppCustomer", "AppCliente",
            "AppComprador", "AppReceptor", "AppBuyer"
        };

        var detectedClientTables = new List<string>();
        foreach (var cand in clientTableCandidates)
        {
            var match = legacyAppTables.FirstOrDefault(t => string.Equals(t, cand, StringComparison.OrdinalIgnoreCase));
            if (match != null && !detectedClientTables.Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                detectedClientTables.Add(match);
            }
        }

        if (detectedClientTables.Count == 0)
        {
            var fuzzy = legacyAppTables.Where(t =>
                t.Contains("Client", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Customer", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Comprador", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Receptor", StringComparison.OrdinalIgnoreCase)).ToList();

            detectedClientTables.AddRange(fuzzy);
        }

        if (detectedClientTables.Count == 0)
        {
            _logger.LogInformation("No se detectó tabla específica de clientes/compradores en LegacyDb. Se usarán únicamente compradores de facturas.");
            return;
        }

        int totalCompradoresExtraidos = 0;
        foreach (var clientTableName in detectedClientTables)
        {
            _logger.LogInformation("Analizando tabla de compradores: '{Table}'...", clientTableName);

            var clientCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdCols = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName;", connection))
            {
                cmdCols.Parameters.AddWithValue("@TableName", clientTableName);
                using var rCols = await cmdCols.ExecuteReaderAsync(cancellationToken);
                while (await rCols.ReadAsync(cancellationToken))
                {
                    clientCols[rCols.GetString(0)] = rCols.GetString(1);
                }
            }

            var colUser = BuscarColumna(clientCols, "CreatorUserId", "CreatorId", "UserId", "AppUserId", "OwnerId");
            var colIdent = BuscarColumna(clientCols, "IdentificacionComprador", "Identification", "Identificacion", "Ruc", "Cedula", "NumeroIdentificacion", "DocumentNumber", "IdNumber", "Ci", "RucCedula", "Dni");
            var colName = BuscarColumna(clientCols, "RazonSocialComprador", "Name", "RazonSocial", "FullName", "BusinessName", "Nombre", "Descripcion", "ClientName", "CustomerName", "NombreComercial");
            var colTipoId = BuscarColumna(clientCols, "TipoIdentificacionComprador", "IdentificationType", "TipoIdentificacion", "DocType", "TipoDocumento", "Type", "TipoId");
            var colEmail = BuscarColumna(clientCols, "Correo", "Email", "EmailAddress", "CorreoElectronico", "Mail");
            var colAddress = BuscarColumna(clientCols, "DireccionComprador", "Address", "Direccion", "AddressLine", "Dir", "DireccionMatriz", "DireccionCliente");
            var colDeleted = BuscarColumna(clientCols, "IsDeleted", "Deleted", "EstaEliminado");

            if (colUser == null || colIdent == null)
            {
                _logger.LogWarning("La tabla '{Table}' no tiene columnas mínimas para identificar usuario y comprador ({ColUser}, {ColIdent}). Se omite.",
                    clientTableName, colUser ?? "NO ENCONTRADA", colIdent ?? "NO ENCONTRADA");
                continue;
            }

            string selectClause = $@"
                SELECT 
                    [{colUser}] AS CreatorId,
                    [{colIdent}] AS Identificacion,
                    {(colName != null ? $"[{colName}]" : "''")} AS RazonSocial,
                    {(colTipoId != null ? $"[{colTipoId}]" : "NULL")} AS TipoIdentificacion,
                    {(colEmail != null ? $"[{colEmail}]" : "NULL")} AS CorreoElectronico,
                    {(colAddress != null ? $"[{colAddress}]" : "NULL")} AS Direccion
                FROM [{clientTableName}]
                WHERE {(colDeleted != null ? $"[{colDeleted}] = 0 AND " : "")} [{colUser}] IS NOT NULL AND [{colIdent}] IS NOT NULL;
            ";

            using var cmd = new SqlCommand(selectClause, connection);
            cmd.CommandTimeout = 120;
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            int compradoresDeEstaTabla = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(reader.GetOrdinal("CreatorId"))) continue;
                var rawCreator = reader["CreatorId"];
                Guid creatorId;
                if (rawCreator is Guid g) creatorId = g;
                else if (rawCreator is string s && Guid.TryParse(s, out var parsedG)) creatorId = parsedG;
                else continue;

                if (!usuariosMap.TryGetValue(creatorId, out var clienteDto)) continue;

                var rawIdent = reader.IsDBNull(reader.GetOrdinal("Identificacion")) ? "" : reader["Identificacion"].ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(rawIdent)) continue;

                var rawName = reader.IsDBNull(reader.GetOrdinal("RazonSocial")) ? "" : reader["RazonSocial"].ToString()?.Trim() ?? "";
                var rawTipoId = reader.IsDBNull(reader.GetOrdinal("TipoIdentificacion")) ? null : reader["TipoIdentificacion"].ToString()?.Trim();
                var rawEmail = reader.IsDBNull(reader.GetOrdinal("CorreoElectronico")) ? null : reader["CorreoElectronico"].ToString()?.Trim();
                var rawAddress = reader.IsDBNull(reader.GetOrdinal("Direccion")) ? null : reader["Direccion"].ToString()?.Trim();

                var tipoIdNormalizado = NormalizarTipoIdentificacion(rawTipoId, rawIdent);

                clienteDto.Compradores.Add(new CompradorMigracionDto
                {
                    TipoIdentificacion = tipoIdNormalizado,
                    Identificacion = rawIdent.Length <= 20 ? rawIdent : rawIdent[..20],
                    RazonSocial = !string.IsNullOrWhiteSpace(rawName)
                        ? (rawName.Length <= 300 ? rawName : rawName[..300])
                        : (tipoIdNormalizado == "07" ? "CONSUMIDOR FINAL" : rawIdent),
                    CorreoElectronico = !string.IsNullOrWhiteSpace(rawEmail)
                        ? (rawEmail.Length <= 300 ? rawEmail : rawEmail[..300])
                        : null,
                    Direccion = !string.IsNullOrWhiteSpace(rawAddress)
                        ? (rawAddress.Length <= 300 ? rawAddress : rawAddress[..300])
                        : null
                });

                compradoresDeEstaTabla++;
                totalCompradoresExtraidos++;
            }

            _logger.LogInformation("Se extrajeron {Count} compradores directos desde '{Table}'.", compradoresDeEstaTabla, clientTableName);
        }

        _logger.LogInformation("Total de compradores directos extraídos de catálogos LegacyDb: {Total}.", totalCompradoresExtraidos);
    }

    private async Task ExtraerProductosDirectosAsync(
        SqlConnection connection,
        List<string> legacyAppTables,
        Dictionary<Guid, ClienteMigracionDto> usuariosMap,
        CancellationToken cancellationToken)
    {
        var prodTableCandidates = new[]
        {
            "AppDetails", "AppProducts", "AppItems", "AppProductos", "AppServices", "AppServicios",
            "AppArticulos", "AppMercaderias", "AppProduct", "AppItem", "AppProducto",
            "AppService", "AppServicio", "AppArticulo"
        };

        var detectedProdTables = new List<string>();
        foreach (var cand in prodTableCandidates)
        {
            var match = legacyAppTables.FirstOrDefault(t => string.Equals(t, cand, StringComparison.OrdinalIgnoreCase));
            if (match != null && !detectedProdTables.Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                detectedProdTables.Add(match);
            }
        }

        if (detectedProdTables.Count == 0)
        {
            var fuzzy = legacyAppTables.Where(t =>
                t.Contains("Detail", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Product", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Item", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Producto", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Articulo", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Servic", StringComparison.OrdinalIgnoreCase)).ToList();

            detectedProdTables.AddRange(fuzzy);
        }

        if (detectedProdTables.Count == 0)
        {
            _logger.LogInformation("No se detectó tabla específica de productos/servicios en LegacyDb. Se usarán únicamente productos de facturas.");
            return;
        }

        int totalProductosExtraidos = 0;
        int autoCodeCounter = 1;

        foreach (var prodTableName in detectedProdTables)
        {
            _logger.LogInformation("Analizando tabla de productos/servicios: '{Table}'...", prodTableName);

            var prodCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdCols = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName;", connection))
            {
                cmdCols.Parameters.AddWithValue("@TableName", prodTableName);
                using var rCols = await cmdCols.ExecuteReaderAsync(cancellationToken);
                while (await rCols.ReadAsync(cancellationToken))
                {
                    prodCols[rCols.GetString(0)] = rCols.GetString(1);
                }
            }

            var colId = BuscarColumna(prodCols, "Id");
            var colUser = BuscarColumna(prodCols, "CreatorUserId", "CreatorId", "UserId", "AppUserId", "OwnerId");
            var colCode = BuscarColumna(prodCols, "CodigoAuxiliar", "Code", "Codigo", "CodigoPrincipal", "ItemCode", "ProductCode", "Sku", "MainCode", "CodigoInterno", "Reference");
            var colName = BuscarColumna(prodCols, "Nombre", "Name", "Descripcion", "Description", "ProductName", "ItemName", "Detalle", "Title");
            var colPrice = BuscarColumna(prodCols, "Precio", "Price", "UnitPrice", "PrecioUnitario", "Cost", "Valor", "PrecioVenta", "UnitCost", "Price1");
            var colTaxRateCode = BuscarColumna(prodCols, "CodigoPorcentaje", "TaxPercentageCode", "PorcentajeCodigo", "IvaCodigo", "CodigoIva");
            var colTaxCode = BuscarColumna(prodCols, "CodigoImpuesto", "TaxCode", "ImpuestoCodigo");
            var colTaxRate = BuscarColumna(prodCols, "Tarifa", "TaxRate", "PorcentajeIva", "Porcentaje", "IvaRate", "Iva", "Tax", "TarifaIva");
            var colDeleted = BuscarColumna(prodCols, "IsDeleted", "Deleted", "EstaEliminado");

            if (colUser == null || (colCode == null && colName == null))
            {
                _logger.LogWarning("La tabla '{Table}' no tiene columnas mínimas para identificar usuario y producto ({ColUser}, {ColItem}). Se omite.",
                    prodTableName, colUser ?? "NO ENCONTRADA", colCode ?? colName ?? "NO ENCONTRADA");
                continue;
            }

            string selectClause = $@"
                SELECT 
                    {(colId != null ? $"[{colId}]" : "NULL")} AS ItemId,
                    [{colUser}] AS CreatorId,
                    {(colCode != null ? $"[{colCode}]" : "''")} AS CodigoPrincipal,
                    {(colName != null ? $"[{colName}]" : "''")} AS Descripcion,
                    {(colPrice != null ? $"[{colPrice}]" : "0")} AS PrecioUnitario,
                    {(colTaxRate != null ? $"[{colTaxRate}]" : "NULL")} AS Tarifa,
                    {(colTaxRateCode != null ? $"[{colTaxRateCode}]" : "NULL")} AS CodigoPorcentaje,
                    {(colTaxCode != null ? $"[{colTaxCode}]" : "NULL")} AS CodigoImpuesto
                FROM [{prodTableName}]
                WHERE {(colDeleted != null ? $"[{colDeleted}] = 0 AND " : "")} [{colUser}] IS NOT NULL;
            ";

            using var cmd = new SqlCommand(selectClause, connection);
            cmd.CommandTimeout = 120;
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            int productosDeEstaTabla = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(reader.GetOrdinal("CreatorId"))) continue;
                var rawCreator = reader["CreatorId"];
                Guid creatorId;
                if (rawCreator is Guid g) creatorId = g;
                else if (rawCreator is string s && Guid.TryParse(s, out var parsedG)) creatorId = parsedG;
                else continue;

                if (!usuariosMap.TryGetValue(creatorId, out var clienteDto)) continue;

                var rawCode = reader.IsDBNull(reader.GetOrdinal("CodigoPrincipal")) ? "" : reader["CodigoPrincipal"].ToString()?.Trim() ?? "";
                var rawDesc = reader.IsDBNull(reader.GetOrdinal("Descripcion")) ? "" : reader["Descripcion"].ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(rawCode) && string.IsNullOrWhiteSpace(rawDesc)) continue;

                if (string.IsNullOrWhiteSpace(rawCode))
                {
                    if (!reader.IsDBNull(reader.GetOrdinal("ItemId")))
                    {
                        var rawIdObj = reader["ItemId"];
                        if (rawIdObj is Guid itemIdGuid)
                        {
                            rawCode = $"D-{itemIdGuid.ToString("N")[..8].ToUpperInvariant()}";
                        }
                        else
                        {
                            rawCode = $"PROD-{autoCodeCounter++:D4}";
                        }
                    }
                    else
                    {
                        rawCode = $"PROD-{autoCodeCounter++:D4}";
                    }
                }
                if (string.IsNullOrWhiteSpace(rawDesc))
                {
                    rawDesc = rawCode;
                }

                decimal precio = 0m;
                if (!reader.IsDBNull(reader.GetOrdinal("PrecioUnitario")))
                {
                    var priceObj = reader["PrecioUnitario"];
                    if (priceObj is decimal d) precio = d;
                    else if (priceObj is double dbl) precio = (decimal)dbl;
                    else if (priceObj is float flt) precio = (decimal)flt;
                    else if (decimal.TryParse(priceObj.ToString(), out var dp)) precio = dp;
                }
                if (precio < 0m) precio = 0m;

                decimal? rawTarifa = null;
                if (!reader.IsDBNull(reader.GetOrdinal("Tarifa")))
                {
                    var tObj = reader["Tarifa"];
                    if (tObj is decimal td) rawTarifa = td;
                    else if (tObj is double tdbl) rawTarifa = (decimal)tdbl;
                    else if (decimal.TryParse(tObj.ToString(), out var tdp)) rawTarifa = tdp;
                }

                var rawCodPorc = reader.IsDBNull(reader.GetOrdinal("CodigoPorcentaje")) ? null : reader["CodigoPorcentaje"].ToString()?.Trim();
                var rawCodImp = reader.IsDBNull(reader.GetOrdinal("CodigoImpuesto")) ? "2" : reader["CodigoImpuesto"].ToString()?.Trim() ?? "2";

                var (codPorcFinal, tarifaFinal) = DeterminarIva(rawCodPorc, rawTarifa);

                var prodDto = new DetalleFacturaMigracionDto
                {
                    CodigoPrincipal = rawCode.Length <= 25 ? rawCode : rawCode[..25],
                    Descripcion = rawDesc.Length <= 300 ? rawDesc : rawDesc[..300],
                    Cantidad = 1m,
                    PrecioUnitario = precio,
                    Descuento = 0m,
                    Impuestos = new List<ImpuestoMigracionDto>
                    {
                        new ImpuestoMigracionDto
                        {
                            Codigo = string.IsNullOrWhiteSpace(rawCodImp) ? "2" : rawCodImp,
                            CodigoPorcentaje = codPorcFinal,
                            Tarifa = tarifaFinal,
                            BaseImponible = precio,
                            Valor = Math.Round(precio * (tarifaFinal / 100m), 2, MidpointRounding.AwayFromZero)
                        }
                    }
                };

                clienteDto.Productos.Add(prodDto);
                productosDeEstaTabla++;
                totalProductosExtraidos++;
            }

            _logger.LogInformation("Se extrajeron {Count} productos/servicios directos desde '{Table}'.", productosDeEstaTabla, prodTableName);
        }

        _logger.LogInformation("Total de productos/servicios directos extraídos de catálogos LegacyDb: {Total}.", totalProductosExtraidos);
    }

    public static string? BuscarColumna(Dictionary<string, string> columnas, params string[] candidatos)
    {
        foreach (var c in candidatos)
        {
            foreach (var key in columnas.Keys)
            {
                if (string.Equals(key, c, StringComparison.OrdinalIgnoreCase))
                    return key;
            }
        }
        return null;
    }

    public static string NormalizarTipoIdentificacion(string? rawTipoId, string identificacion)
    {
        var trimmed = rawTipoId?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            if (trimmed is "04" or "05" or "06" or "07" or "08")
                return trimmed;

            if (trimmed.Contains("RUC")) return "04";
            if (trimmed.Contains("CEDULA") || trimmed.Contains("CÉDULA")) return "05";
            if (trimmed.Contains("PASAPORTE")) return "06";
            if (trimmed.Contains("CONSUMIDOR") || trimmed.Contains("FINAL")) return "07";
            if (trimmed.Contains("EXTERIOR")) return "08";
        }

        var idLimpia = identificacion.Trim();
        if (idLimpia == "9999999999999") return "07";
        if (idLimpia.Length == 13 && idLimpia.All(char.IsDigit)) return "04";
        if (idLimpia.Length == 10 && idLimpia.All(char.IsDigit)) return "05";

        return "07";
    }

    public static (string CodigoPorcentaje, decimal Tarifa) DeterminarIva(string? rawCodPorcentaje, decimal? rawTarifa)
    {
        // Si viene tarifa como fracción decimal (ej: 0.15 o 0.12)
        if (rawTarifa is > 0 and <= 1)
        {
            rawTarifa *= 100m;
        }

        if (!string.IsNullOrWhiteSpace(rawCodPorcentaje))
        {
            var cp = rawCodPorcentaje.Trim();
            if (cp == "4") return ("4", rawTarifa ?? 15.00m);
            if (cp == "2") return ("2", rawTarifa ?? 12.00m);
            if (cp == "0") return ("0", 0.00m);
            if (cp == "5") return ("5", rawTarifa ?? 5.00m);
            if (cp == "10") return ("10", rawTarifa ?? 13.00m);
            if (cp == "3") return ("3", rawTarifa ?? 14.00m);
            return (cp, rawTarifa ?? 15.00m);
        }

        if (rawTarifa.HasValue)
        {
            var t = Math.Round(rawTarifa.Value, 2);
            if (t == 15.00m) return ("4", 15.00m);
            if (t == 12.00m) return ("2", 12.00m);
            if (t == 0.00m) return ("0", 0.00m);
            if (t == 5.00m) return ("5", 5.00m);
            if (t == 13.00m) return ("10", 13.00m);
            if (t == 14.00m) return ("3", 14.00m);
            return ("4", t);
        }

        // Tarifa por defecto vigente en SRI Ecuador: 15% (código 4)
        return ("4", 15.00m);
    }
}
