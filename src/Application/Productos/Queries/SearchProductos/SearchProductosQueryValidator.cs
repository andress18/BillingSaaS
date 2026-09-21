namespace BillingSaaS.Application.Productos.Queries.SearchProductos;

public class SearchProductosQueryValidator : AbstractValidator<SearchProductosQuery>
{
    public SearchProductosQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("El límite de resultados debe estar entre 1 y 100.");
    }
}

