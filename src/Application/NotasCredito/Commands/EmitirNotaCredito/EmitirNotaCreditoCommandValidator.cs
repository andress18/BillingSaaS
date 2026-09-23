namespace BillingSaaS.Application.NotasCredito.Commands.EmitirNotaCredito;

public class EmitirNotaCreditoCommandValidator : AbstractValidator<EmitirNotaCreditoCommand>
{
    public EmitirNotaCreditoCommandValidator()
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

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .MaximumLength(300)
            .WithMessage("El motivo de la modificación es obligatorio y no puede exceder 300 caracteres.");

        RuleFor(x => x.Detalles)
            .NotEmpty()
            .WithMessage("La nota de crédito debe contener al menos un detalle de producto o servicio.");

        RuleForEach(x => x.Detalles).ChildRules(detalle =>
        {
            detalle.RuleFor(d => d.CodigoPrincipal)
                .NotEmpty()
                .MaximumLength(25)
                .WithMessage("El código principal del detalle no puede estar vacío ni superar 25 caracteres.");

            detalle.RuleFor(d => d.Descripcion)
                .NotEmpty()
                .MaximumLength(300)
                .WithMessage("La descripción del detalle no puede estar vacía ni superar 300 caracteres.");

            detalle.RuleFor(d => d.Cantidad)
                .GreaterThan(0)
                .WithMessage("La cantidad del detalle debe ser mayor a cero.");

            detalle.RuleFor(d => d.PrecioUnitario)
                .GreaterThanOrEqualTo(0)
                .WithMessage("El precio unitario no puede ser negativo.");

            detalle.RuleFor(d => d.Descuento)
                .GreaterThanOrEqualTo(0)
                .WithMessage("El descuento no puede ser negativo.");

            detalle.RuleFor(d => d.Impuestos)
                .NotEmpty()
                .WithMessage("Cada detalle debe tener al menos un impuesto asociado.");

            detalle.RuleForEach(d => d.Impuestos).ChildRules(impuesto =>
            {
                impuesto.RuleFor(i => i.Codigo)
                    .NotEmpty()
                    .WithMessage("El código de impuesto es obligatorio (ej. 2 para IVA).");

                impuesto.RuleFor(i => i.CodigoPorcentaje)
                    .NotEmpty()
                    .WithMessage("El código de porcentaje de impuesto es obligatorio.");

                impuesto.RuleFor(i => i.BaseImponible)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("La base imponible del impuesto no puede ser negativa.");
            });
        });
    }
}

