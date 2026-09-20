using FluentValidation;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Application.Validators;

public class AbonoCreditoCreateDtoValidator : AbstractValidator<AbonoCreditoCreateDto>
{
    public AbonoCreditoCreateDtoValidator()
    {
        RuleFor(x => x.Monto).GreaterThan(0).WithMessage("El abono debe ser mayor a cero.");
        RuleFor(x => x.Metodo).NotEmpty().WithMessage("Seleccione el método de pago.")
            .Must(m => CreditoService.MetodosAbono.Contains(m.Trim().ToUpperInvariant()))
            .WithMessage("Método de pago inválido. Use EFECTIVO, TARJETA o TRANSFERENCIA.");
        RuleFor(x => x.Referencia).MaximumLength(100).WithMessage("La referencia no puede superar 100 caracteres.");
    }
}
