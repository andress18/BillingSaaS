using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BillingSaaS.MigrationTool.Services;

public record PartnerCsvEntry(
    string Identificador,
    string PasswordUsuario,
    string? PasswordFirma);

public interface IPartnerCsvFilterService
{
    bool IsFilterActive { get; }
    int TotalEntradasCargadas { get; }
    void CargarCsv(string? filePath);
    bool TryGetEntry(string? userName, string? email, string? ruc, out PartnerCsvEntry? entry);
}

public class PartnerCsvFilterService : IPartnerCsvFilterService
{
    private readonly ILogger<PartnerCsvFilterService> _logger;
    private readonly Dictionary<string, PartnerCsvEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool IsFilterActive => _entries.Count > 0;
    public int TotalEntradasCargadas => _entries.Count;

    public PartnerCsvFilterService(ILogger<PartnerCsvFilterService> logger)
    {
        _logger = logger;
    }

    public void CargarCsv(string? filePath)
    {
        _entries.Clear();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogInformation("No se especificó archivo CSV de filtrado de clientes (FilterCsvPath vacío). Se procesarán todos los clientes del origen.");
            return;
        }

        string fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(AppContext.BaseDirectory, filePath);

        if (!File.Exists(fullPath))
        {
            // Intentar buscar también en el directorio actual de ejecución
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("El archivo CSV de filtrado especificado no existe: '{Path}'. Se procesarán todos los clientes.", filePath);
            return;
        }

        _logger.LogInformation("Cargando lista de clientes y contraseñas desde CSV: '{Path}'...", fullPath);

        var lines = File.ReadAllLines(fullPath)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#") && !l.TrimStart().StartsWith("//"))
            .ToList();

        if (lines.Count == 0)
        {
            _logger.LogWarning("El archivo CSV '{Path}' está vacío.", fullPath);
            return;
        }

        // Detectar delimitador (coma o punto y coma)
        char delimiter = lines[0].Contains(';') ? ';' : ',';

        int colIdentificador = 0;
        int colPassword = 1;
        int colPasswordFirma = -1;

        int startIndex = 0;
        var header = SplitCsvLine(lines[0], delimiter);

        // Detectar encabezados si existen
        bool hasHeaders = false;
        for (int i = 0; i < header.Count; i++)
        {
            var h = header[i].Trim().ToLowerInvariant();
            if (h is "usuario" or "username" or "user" or "email" or "correo" or "ruc" or "identificador")
            {
                colIdentificador = i;
                hasHeaders = true;
            }
            else if (h is "password" or "contraseña" or "contrasena" or "clave" or "pass" or "passwordusuario")
            {
                colPassword = i;
                hasHeaders = true;
            }
            else if (h is "passwordfirma" or "clavefirma" or "firmapassword" or "signpassword" or "firma")
            {
                colPasswordFirma = i;
                hasHeaders = true;
            }
        }

        if (hasHeaders)
        {
            startIndex = 1;
        }
        else
        {
            // Sin encabezado explícito: col 0 = Usuario, col 1 = Password, col 2 = PasswordFirma (si existe)
            if (header.Count > 2)
            {
                colPasswordFirma = 2;
            }
        }

        for (int i = startIndex; i < lines.Count; i++)
        {
            var columns = SplitCsvLine(lines[i], delimiter);
            if (columns.Count <= colIdentificador || string.IsNullOrWhiteSpace(columns[colIdentificador]))
            {
                continue;
            }

            var identificador = columns[colIdentificador].Trim();
            var password = columns.Count > colPassword ? columns[colPassword].Trim() : string.Empty;
            string? passwordFirma = colPasswordFirma >= 0 && columns.Count > colPasswordFirma && !string.IsNullOrWhiteSpace(columns[colPasswordFirma])
                ? columns[colPasswordFirma].Trim()
                : null;

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Línea {Line}: Cliente '{Id}' no tiene contraseña asignada en el CSV.", i + 1, identificador);
            }

            var entry = new PartnerCsvEntry(identificador, password, passwordFirma);

            _entries[identificador] = entry;

            // Si el identificador es correo, también indexar por el alias antes del '@'
            if (identificador.Contains('@'))
            {
                var alias = identificador.Split('@')[0].Trim();
                if (!string.IsNullOrWhiteSpace(alias) && !_entries.ContainsKey(alias))
                {
                    _entries[alias] = entry;
                }
            }
        }

        _logger.LogInformation("Se cargaron {Count} clientes válidos desde el archivo CSV para migración selectiva.", _entries.Values.Distinct().Count());
    }

    public bool TryGetEntry(string? userName, string? email, string? ruc, out PartnerCsvEntry? entry)
    {
        entry = null;
        if (!IsFilterActive) return false;

        if (!string.IsNullOrWhiteSpace(userName) && _entries.TryGetValue(userName.Trim(), out entry))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(email) && _entries.TryGetValue(email.Trim(), out entry))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ruc) && _entries.TryGetValue(ruc.Trim(), out entry))
        {
            return true;
        }

        return false;
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
