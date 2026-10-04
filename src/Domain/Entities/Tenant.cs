using System;
using BillingSaaS.Domain.Constants;

namespace BillingSaaS.Domain.Entities;

public class Tenant : BaseAuditableEntity
{
    public new Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public bool Activo { get; private set; } = true;
    public string Estado { get; private set; } = TenantEstados.PendientePago;
    public string? TokenActivacion { get; private set; }
    public DateTime? FechaActivacion { get; private set; }

    /// <summary>
    /// Identificador del Socio / Partner que refirió o gestiona este negocio (null si es cliente directo)
    /// </summary>
    public Guid? PartnerId { get; private set; }

    private Tenant() { }

    public static Tenant Crear(
        string nombre,
        Guid? partnerId = null,
        Guid? id = null,
        string estado = TenantEstados.PendientePago)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del tenant es obligatorio.", nameof(nombre));

        var estadoNormalizado = string.IsNullOrWhiteSpace(estado)
            ? TenantEstados.PendientePago
            : estado.Trim().ToUpperInvariant();

        return new Tenant
        {
            Id = id ?? Guid.NewGuid(),
            Nombre = nombre.Trim(),
            PartnerId = partnerId,
            Activo = true,
            Estado = estadoNormalizado,
            TokenActivacion = estadoNormalizado == TenantEstados.PendientePago
                ? Guid.NewGuid().ToString("N").ToUpperInvariant()
                : null,
            FechaActivacion = estadoNormalizado == TenantEstados.Activo ? DateTime.UtcNow : null
        };
    }

    public void ActualizarNombre(string nuevoNombre)
    {
        if (string.IsNullOrWhiteSpace(nuevoNombre))
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(nuevoNombre));

        Nombre = nuevoNombre.Trim();
    }

    public void AsignarPartner(Guid partnerId) => PartnerId = partnerId;

    public void Activar(DateTime fechaActivacion)
    {
        Estado = TenantEstados.Activo;
        Activo = true;
        FechaActivacion = fechaActivacion;
        TokenActivacion = null;
    }

    public void Activar() => Activar(DateTime.UtcNow);

    public void MarcarPendientePago()
    {
        Estado = TenantEstados.PendientePago;
        TokenActivacion ??= Guid.NewGuid().ToString("N").ToUpperInvariant();
    }

    public void Desactivar()
    {
        Activo = false;
        Estado = TenantEstados.Inactivo;
    }
}
