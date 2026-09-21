namespace BillingSaaS.Application.Productos.Commands.CrearProducto;

public class CrearProductoCommandValidator : AbstractValidator<CrearProductoCommand>
{
    public CrearProductoCommandValidator()
    {
        RuleFor(x => x.CodigoPrincipal)
            .NotEmpty().WithMessage("El código principal del producto es obligatorio.")
            .MaximumLength(25).WithMessage("El código principal no puede exceder los 25 caracteres.");

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción del producto es obligatoria.")
            .MaximumLength(300).WithMessage("La descripción no puede exceder los 300 caracteres.");

        RuleFor(x => x.PrecioUnitario)
            .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");

        RuleFor(x => x.CodigoImpuesto)
            .NotEmpty().WithMessage("El código de impuesto es obligatorio.")
            .MaximumLength(10).WithMessage("El código de impuesto no puede exceder los 10 caracteres.");

        RuleFor(x => x.CodigoPorcentaje)
            .NotEmpty().WithMessage("El código de porcentaje es obligatorio.")
            .MaximumLength(10).WithMessage("El código de porcentaje no puede exceder los 10 caracteres.");

        RuleFor(x => x.Tarifa)
            .GreaterThanOrEqualTo(0).WithMessage("La tarifa de impuesto no puede ser negativa.");
    }
}

