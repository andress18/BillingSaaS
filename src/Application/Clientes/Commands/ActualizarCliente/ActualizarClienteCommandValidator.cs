namespace BillingSaaS.Application.Clientes.Commands.ActualizarCliente;

public class ActualizarClienteCommandValidator : AbstractValidator<ActualizarClienteCommand>
{
    public ActualizarClienteCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El identificador del cliente es obligatorio.");

        RuleFor(x => x.TipoIdentificacion)
            .NotEmpty().WithMessage("El tipo de identificación es obligatorio.")
            .Must(t => t is "04" or "05" or "06" or "07" or "08")
            .WithMessage("El tipo de identificación debe ser válido (04: RUC, 05: Cédula, 06: Pasaporte, 07: Consumidor Final, 08: Exterior).");

        RuleFor(x => x.RazonSocial)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(300).WithMessage("La razón social no puede exceder los 300 caracteres.");

        RuleFor(x => x.Direccion)
            .MaximumLength(300).WithMessage("La dirección no puede exceder los 300 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Direccion));

        RuleFor(x => x.CorreoElectronico)
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoElectronico));
    }
}

