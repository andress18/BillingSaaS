namespace BillingSaaS.Application.Facturas.Queries.GetFacturas;

public class GetFacturasQueryValidator : AbstractValidator<GetFacturasQuery>
{
    public GetFacturasQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .When(x => x.PageNumber.HasValue)
            .WithMessage("PageNumber at least greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .When(x => x.PageSize.HasValue)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}
