using FluentValidation;

namespace BillingSaaS.Application.Suscripciones.Commands.SolicitarRenovacion;

public class SolicitarRenovacionCommandValidator : AbstractValidator<SolicitarRenovacionCommand>
{
    public SolicitarRenovacionCommandValidator()
    {
        RuleFor(v => v.PlanId)
            .GreaterThan(0)
            .WithMessage("Debe seleccionar un plan válido.");

        RuleFor(v => v.Frecuencia)
            .NotEmpty()
            .Must(f => f.Trim().Equals("MENSUAL", System.StringComparison.OrdinalIgnoreCase)
                    || f.Trim().Equals("ANUAL", System.StringComparison.OrdinalIgnoreCase))
            .WithMessage("La frecuencia debe ser 'MENSUAL' o 'ANUAL'.");

        RuleFor(v => v.NumeroComprobante)
            .NotEmpty()
            .WithMessage("El número de comprobante o referencia es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El número de comprobante no puede superar los 100 caracteres.");
    }
}

