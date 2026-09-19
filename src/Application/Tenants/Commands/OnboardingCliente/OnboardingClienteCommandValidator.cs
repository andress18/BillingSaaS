using FluentValidation;

namespace BillingSaaS.Application.Tenants.Commands.OnboardingCliente;

public class OnboardingClienteCommandValidator : AbstractValidator<OnboardingClienteCommand>
{
    public OnboardingClienteCommandValidator()
    {
        RuleFor(v => v.NombreOrganizacion)
            .NotEmpty()
            .WithMessage("El nombre de la organización o cliente es obligatorio.")
            .MaximumLength(200)
            .WithMessage("El nombre de la organización no puede superar los 200 caracteres.");

        RuleFor(v => v.Email)
            .NotEmpty()
            .WithMessage("El correo electrónico del cliente es obligatorio.")
            .EmailAddress()
            .WithMessage("Debe proporcionar una dirección de correo electrónico válida.");

        When(v => !string.IsNullOrWhiteSpace(v.Ruc), () =>
        {
            RuleFor(v => v.Ruc)
                .Matches(@"^\d{13}$")
                .WithMessage("El RUC debe contener exactamente 13 dígitos numéricos.");
        });

        When(v => !string.IsNullOrWhiteSpace(v.CertificadoP12Base64), () =>
        {
            RuleFor(v => v.PasswordCertificado)
                .NotEmpty()
                .WithMessage("Si se adjunta el certificado digital, la contraseña del certificado es obligatoria.");

            RuleFor(v => v.Ruc)
                .NotEmpty()
                .WithMessage("Para configurar el certificado digital, el RUC del emisor es obligatorio.");
        });
    }
}

