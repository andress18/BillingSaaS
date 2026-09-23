namespace BillingSaaS.Application.NotasCredito.Queries.GetNotasCredito;

public class GetNotasCreditoQueryValidator : AbstractValidator<GetNotasCreditoQuery>
{
    public GetNotasCreditoQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .When(x => x.PageNumber.HasValue)
            .WithMessage("PageNumber debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .When(x => x.PageSize.HasValue)
            .WithMessage("PageSize debe estar entre 1 y 100.");
    }
}

