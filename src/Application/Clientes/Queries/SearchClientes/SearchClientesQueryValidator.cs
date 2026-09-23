namespace BillingSaaS.Application.Clientes.Queries.SearchClientes;

public class SearchClientesQueryValidator : AbstractValidator<SearchClientesQuery>
{
    public SearchClientesQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("El límite de resultados debe estar entre 1 y 100.");
    }
}

