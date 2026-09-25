namespace BillingSaaS.Application.Emisores.Commands.ConfigurarEmisor;

public class ConfigurarEmisorCommandValidator : AbstractValidator<ConfigurarEmisorCommand>
{
    public ConfigurarEmisorCommandValidator()
    {
        RuleFor(x => x.Ruc)
            .NotEmpty()
            .Length(13)
            .Matches("^[0-9]+$")
            .WithMessage("El RUC debe contener exactamente 13 dígitos numéricos.");

        RuleFor(x => x.RazonSocial)
            .NotEmpty()
            .MaximumLength(300)
            .WithMessage("La razón social es obligatoria y no puede superar 300 caracteres.");

        RuleFor(x => x.DireccionMatriz)
            .NotEmpty()
            .MaximumLength(300)
            .WithMessage("La dirección matriz es obligatoria y no puede superar 300 caracteres.");

        // RuleFor(x => x.CodigoEstablecimiento)
        //     .NotEmpty()
        //     .Equal("001")
        //     .WithMessage("El plan solo permite el establecimiento principal '001'.");

        // RuleFor(x => x.PuntoEmision)
        //     .NotEmpty()
        //     .Equal("001")
        //     .WithMessage("El plan solo permite el punto de emisión '001'.");

        RuleFor(x => x.Ambiente)
            .Must(a => a is 1 or 2)
            .WithMessage("El ambiente debe ser 1 (Pruebas) o 2 (Producción).");

        RuleFor(x => x.RegimenRimpe)
            .Must(r => string.IsNullOrWhiteSpace(r) || Domain.Constants.RegimenRimpeTipos.EsValido(r))
            .WithMessage("El régimen tributario debe ser 'CONTRIBUYENTE RÉGIMEN RIMPE', 'CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE' o vacío para Régimen General.");
    }
}

