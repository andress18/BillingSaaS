namespace BillingSaaS.Application.Clientes.Queries.SearchClientes;

public class ClienteLookupDto
{
    public Guid Id { get; init; }
    public string TipoIdentificacion { get; init; } = null!;
    public string Identificacion { get; init; } = null!;
    public string RazonSocial { get; init; } = null!;
    public string? Direccion { get; init; }
    public string? CorreoElectronico { get; init; }
    public bool Activo { get; init; }
}

