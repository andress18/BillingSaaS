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

        RuleFor(v => v)
            .Must(v => !string.IsNullOrWhiteSpace(v.Email) || !string.IsNullOrWhiteSpace(v.Username))
            .WithMessage("Debe proporcionar al menos un correo electrónico o un nombre de usuario.");

        When(v => !string.IsNullOrWhiteSpace(v.Email), () =>
        {
            RuleFor(v => v.Email)
                .EmailAddress()
                .WithMessage("Debe proporcionar una dirección de correo electrónico válida.");
        });

        When(v => !string.IsNullOrWhiteSpace(v.Username), () =>
        {
            RuleFor(v => v.Username)
                .MinimumLength(3)
                .WithMessage("El nombre de usuario debe tener al menos 3 caracteres.")
                .MaximumLength(100)
                .WithMessage("El nombre de usuario no puede superar los 100 caracteres.")
                .Matches(@"^[a-zA-Z0-9._@+-]+$")
                .WithMessage("El nombre de usuario contiene caracteres no válidos.");
        });

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

