namespace BillingSaaS.Application.Suscripciones.Commands.RenovarSuscripcion;

public class RenovarSuscripcionCommandValidator : AbstractValidator<RenovarSuscripcionCommand>
{
    public RenovarSuscripcionCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .GreaterThan(0)
            .WithMessage("El identificador del plan debe ser válido.");

        RuleFor(x => x.Frecuencia)
            .NotEmpty()
            .Must(f => f.Trim().Equals("MENSUAL", System.StringComparison.OrdinalIgnoreCase) 
                    || f.Trim().Equals("ANUAL", System.StringComparison.OrdinalIgnoreCase))
            .WithMessage("La frecuencia debe ser 'MENSUAL' o 'ANUAL'.");
    }
}
