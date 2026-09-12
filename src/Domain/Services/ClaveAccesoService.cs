using System;

namespace BillingSaaS.Domain.Services;

public static class ClaveAccesoService
{
    public static string GenerarDigitoVerificador(string claveAcceso48)
    {
        if (string.IsNullOrWhiteSpace(claveAcceso48) || claveAcceso48.Length != 48)
        {
            throw new ArgumentException(
                "La cadena base para la clave de acceso debe tener exactamente 48 dígitos antes de calcular el verificador.");
        }

        int suma = 0;
        int factor = 2;

        // El algoritmo del SRI multiplica de derecha a izquierda (factor de 2 a 7)
        for (int i = claveAcceso48.Length - 1; i >= 0; i--)
        {
            // Restar '0' convierte el char a int sin asignar memoria extra
            suma += (claveAcceso48[i] - '0') * factor;

            factor = factor == 7 ? 2 : factor + 1;
        }

        int residuo = suma % 11;
        int digitoVerificador = 11 - residuo;

        // Reglas explícitas del SRI para los resultados 11 y 10
        if (digitoVerificador == 11)
        {
            digitoVerificador = 0;
        }
        else if (digitoVerificador == 10)
        {
            digitoVerificador = 1;
        }

        return $"{claveAcceso48}{digitoVerificador}";
    }
}
