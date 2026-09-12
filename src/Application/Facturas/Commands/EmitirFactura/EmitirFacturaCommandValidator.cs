namespace BillingSaaS.Application.Factura.Commands.EmitirFactura;

public class EmitirFacturaCommandValidator : AbstractValidator<EmitirFacturaCommand>
{
    public EmitirFacturaCommandValidator()
    {
        // Validaciones estructurales y de longitudes obligatorias según la Ficha Técnica del SRI

        RuleFor(x => x.Establecimiento)
            .NotEmpty()
            .Length(3)
            .WithMessage("El código del establecimiento emisor debe tener exactamente 3 dígitos numéricos."); //

        RuleFor(x => x.PuntoEmision)
            .NotEmpty()
            .Length(3).WithMessage("El código del punto de emisión debe tener exactamente 3 dígitos numéricos."); //

        RuleFor(x => x.Secuencial)
            .NotEmpty()
            .Length(9).WithMessage(
                "El número secuencial debe tener exactamente 9 dígitos. Si no los completa, rellenar con ceros a la izquierda."); //[cite: 1]

        RuleFor(x => x.RucEmisor)
            .NotEmpty()
            .Length(13).WithMessage("El número de RUC debe tener exactamente 13 dígitos numéricos."); //[cite: 1]

        RuleFor(x => x.Ambiente)
            .Must(a => a is 1 or 2)
            .WithMessage("El tipo de ambiente debe ser 1 (Pruebas) o 2 (Producción)."); //[cite: 1]

        RuleFor(x => x.TipoEmision)
            .Equal(1)
            .WithMessage(
                "Para el método de autorización offline, solo existe el tipo de emisión normal (1)."); //[cite: 1]

        // Validaciones de forma (Sintaxis típica de la capa de Aplicación)
        RuleFor(x => x.Cliente.CorreoElectronico)
            .EmailAddress().WithMessage("El formato del correo electrónico ingresado no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Cliente.CorreoElectronico));

        RuleFor(x => x.Detalles)
            .NotEmpty().WithMessage("La factura debe contener al menos un detalle de producto o servicio.");

        // Validación en cascada para colecciones (Detalles)
        RuleForEach(x => x.Detalles).ChildRules(detalle =>
        {
            detalle.RuleFor(d => d.CodigoPrincipal)
                .NotEmpty()
                .MaximumLength(25)
                .WithMessage("El código principal del producto no puede exceder los 25 caracteres."); //[cite: 1]

            detalle.RuleFor(d => d.Descripcion)
                .NotEmpty()
                .MaximumLength(300)
                .WithMessage("La descripción del producto no puede exceder los 300 caracteres."); //[cite: 1]

            detalle.RuleFor(d => d.Impuestos)
                .NotEmpty().WithMessage("Cada detalle debe tener un impuesto configurado (ej. IVA).");
        });
    }
}
