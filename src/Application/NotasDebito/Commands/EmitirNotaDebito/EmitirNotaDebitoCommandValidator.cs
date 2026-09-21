namespace BillingSaaS.Application.NotasDebito.Commands.EmitirNotaDebito;

public class EmitirNotaDebitoCommandValidator : AbstractValidator<EmitirNotaDebitoCommand>
{
    public EmitirNotaDebitoCommandValidator()
    {
        RuleFor(x => x.EmisorId)
            .GreaterThan(0)
            .WithMessage("El identificador del emisor debe ser válido.");

        RuleFor(x => x.Cliente)
            .NotNull()
            .WithMessage("Los datos del adquirente/comprador son obligatorios.");

        RuleFor(x => x.Cliente.TipoIdentificacion)
            .NotEmpty()
            .Must(t => t is "04" or "05" or "06" or "07" or "08")
            .WithMessage("El tipo de identificación no es válido (04: RUC, 05: Cédula, 06: Pasaporte, 07: Consumidor Final, 08: Exterior).");

        RuleFor(x => x.Cliente.Identificacion)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage("La identificación del comprador no puede exceder los 20 caracteres.");

        RuleFor(x => x.Cliente.RazonSocial)
            .NotEmpty()
            .MaximumLength(300)
            .WithMessage("La razón social del comprador no puede exceder los 300 caracteres.");

        RuleFor(x => x.Cliente.CorreoElectronico)
            .EmailAddress().WithMessage("El formato del correo electrónico ingresado no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Cliente.CorreoElectronico));

        RuleFor(x => x.NumDocModificado)
            .NotEmpty()
            .Matches(@"^\d{3}-\d{3}-\d{9}$")
            .WithMessage("El número de documento modificado debe tener formato 001-001-000000001.");

        RuleFor(x => x.CodDocModificado)
            .NotEmpty()
            .MaximumLength(2)
            .WithMessage("El código del documento modificado es obligatorio (ej. 01 para Factura).");

        RuleFor(x => x.FechaEmisionDocSustento)
            .NotEmpty()
            .WithMessage("La fecha de emisión del comprobante a modificar es obligatoria.");

        RuleFor(x => x.Motivos)
            .NotEmpty()
            .WithMessage("La nota de débito debe contener al menos un motivo.");

        RuleForEach(x => x.Motivos).ChildRules(motivo =>
        {
            motivo.RuleFor(m => m.Razon)
                .NotEmpty()
                .MaximumLength(300)
                .WithMessage("La razón del débito no puede exceder los 300 caracteres.");

            motivo.RuleFor(m => m.Valor)
                .GreaterThan(0)
                .WithMessage("El valor del motivo de débito debe ser mayor a cero.");
        });

        RuleForEach(x => x.Impuestos).ChildRules(impuesto =>
        {
            impuesto.RuleFor(i => i.Codigo)
                .NotEmpty()
                .WithMessage("El código del impuesto es obligatorio.");

            impuesto.RuleFor(i => i.CodigoPorcentaje)
                .NotEmpty()
                .WithMessage("El código del porcentaje de impuesto es obligatorio.");

            impuesto.RuleFor(i => i.BaseImponible)
                .GreaterThanOrEqualTo(0)
                .WithMessage("La base imponible no puede ser negativa.");
        });
    }
}

