namespace BillingSaaS.Application.Facturas.Commands.EmitirFactura;

public class EmitirFacturaCommandValidator : AbstractValidator<EmitirFacturaCommand>
{
    public EmitirFacturaCommandValidator()
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

        RuleFor(x => x)
            .Must(x =>
            {
                if (x.Cliente == null || x.Detalles == null || !x.Detalles.Any()) return true;
                bool esConsumidorFinal = x.Cliente.TipoIdentificacion == "07" || x.Cliente.Identificacion == "9999999999999";
                if (!esConsumidorFinal) return true;

                decimal total = x.Detalles.Sum(d => (d.Cantidad * d.PrecioUnitario - d.Descuento) + (d.Impuestos?.Sum(i => Math.Round(i.BaseImponible * (i.Tarifa / 100m), 2)) ?? 0m));
                return total <= 50.00m;
            })
            .WithMessage("Las ventas a Consumidor Final no pueden superar los $50.00 USD. Se requieren los datos de identificación del adquirente.");

        RuleFor(x => x.Detalles)
            .NotEmpty().WithMessage("La factura debe contener al menos un detalle de producto o servicio.");

        // Validación en cascada para colecciones (Detalles)
        RuleForEach(x => x.Detalles).ChildRules(detalle =>
        {
            detalle.RuleFor(d => d.CodigoPrincipal)
                .NotEmpty()
                .MaximumLength(25)
                .WithMessage("El código principal del producto no puede exceder los 25 caracteres.");

            detalle.RuleFor(d => d.Descripcion)
                .NotEmpty()
                .MaximumLength(300)
                .WithMessage("La descripción del producto no puede exceder los 300 caracteres.");

            detalle.RuleFor(d => d.Cantidad)
                .GreaterThan(0)
                .WithMessage("La cantidad debe ser mayor a cero.");

            detalle.RuleFor(d => d.PrecioUnitario)
                .GreaterThanOrEqualTo(0)
                .WithMessage("El precio unitario no puede ser negativo.");

            detalle.RuleFor(d => d.Impuestos)
                .NotEmpty().WithMessage("Cada detalle debe tener al menos un impuesto configurado (ej. IVA).");
        });

        RuleFor(x => x.RegimenRimpe)
            .Must(r => string.IsNullOrWhiteSpace(r) || Domain.Constants.RegimenRimpeTipos.EsValido(r))
            .WithMessage("El régimen tributario debe ser 'CONTRIBUYENTE RÉGIMEN RIMPE', 'CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE' o vacío para Régimen General.");

        RuleFor(x => x.CamposAdicionales)
            .Must(c => c == null || c.Count <= 15)
            .WithMessage("El SRI permite un máximo de 15 campos adicionales.");

        RuleForEach(x => x.CamposAdicionales).ChildRules(campo =>
        {
            campo.RuleFor(c => c.Nombre)
                .NotEmpty().WithMessage("El nombre del campo adicional es obligatorio.")
                .MaximumLength(300).WithMessage("El nombre del campo adicional no puede exceder los 300 caracteres.");

            campo.RuleFor(c => c.Valor)
                .NotEmpty().WithMessage("El valor del campo adicional es obligatorio.")
                .MaximumLength(300).WithMessage("El valor del campo adicional no puede exceder los 300 caracteres.");
        });

        RuleFor(x => x.InfoAdicional)
            .Must(c => c == null || c.Count <= 15)
            .WithMessage("El SRI permite un máximo de 15 campos adicionales.");

        RuleForEach(x => x.InfoAdicional).ChildRules(campo =>
        {
            campo.RuleFor(c => c.Nombre)
                .NotEmpty().WithMessage("El nombre del campo adicional es obligatorio.")
                .MaximumLength(300).WithMessage("El nombre del campo adicional no puede exceder los 300 caracteres.");

            campo.RuleFor(c => c.Valor)
                .NotEmpty().WithMessage("El valor del campo adicional es obligatorio.")
                .MaximumLength(300).WithMessage("El valor del campo adicional no puede exceder los 300 caracteres.");
        });
    }
}
