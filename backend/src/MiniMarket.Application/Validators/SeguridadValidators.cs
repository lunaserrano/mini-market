using FluentValidation;
using MiniMarket.Application.DTOs;

namespace MiniMarket.Application.Validators;

public static class PasswordPolicy
{
    public const int LongitudMinima = 8;
    public const int LongitudMaxima = 100; // BCrypt trunca a 72 bytes; el tope evita cargas enormes al hashear.

    /// <summary>Política de contraseñas: 8+ caracteres con mayúscula, minúscula, dígito y símbolo.</summary>
    public static IRuleBuilderOptions<T, string> ContrasenaSegura<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(LongitudMinima).WithMessage($"La contraseña debe tener al menos {LongitudMinima} caracteres.")
            .MaximumLength(LongitudMaxima).WithMessage($"La contraseña no puede superar {LongitudMaxima} caracteres.")
            .Must(p => p.Any(char.IsUpper)).WithMessage("La contraseña debe incluir al menos una mayúscula.")
            .Must(p => p.Any(char.IsLower)).WithMessage("La contraseña debe incluir al menos una minúscula.")
            .Must(p => p.Any(char.IsDigit)).WithMessage("La contraseña debe incluir al menos un número.")
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c))).WithMessage("La contraseña debe incluir al menos un símbolo.");
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("El usuario es obligatorio.").MaximumLength(50);
        // Sin política aquí: en el login solo se exige que venga algo (la política aplica al crearla o cambiarla).
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.").MaximumLength(PasswordPolicy.LongitudMaxima);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("El refresh token es obligatorio.").MaximumLength(200);
    }
}

public class CambiarPasswordRequestValidator : AbstractValidator<CambiarPasswordRequest>
{
    public CambiarPasswordRequestValidator()
    {
        RuleFor(x => x.PasswordActual).NotEmpty().WithMessage("La contraseña actual es obligatoria.").MaximumLength(PasswordPolicy.LongitudMaxima);
        RuleFor(x => x.PasswordNueva).ContrasenaSegura();
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NuevaPassword).ContrasenaSegura();
    }
}

public class UsuarioCreateDtoValidator : AbstractValidator<UsuarioCreateDto>
{
    public UsuarioCreateDtoValidator()
    {
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(150);
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .Length(3, 50).WithMessage("El usuario debe tener entre 3 y 50 caracteres.")
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("El usuario solo puede contener letras, números, punto, guion y guion bajo.");
        RuleFor(x => x.Password).ContrasenaSegura();
        RuleFor(x => x.RolId).GreaterThan(0).WithMessage("Seleccione un rol.");
    }
}

public class UsuarioUpdateDtoValidator : AbstractValidator<UsuarioUpdateDto>
{
    public UsuarioUpdateDtoValidator()
    {
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(150);
        RuleFor(x => x.RolId).GreaterThan(0).WithMessage("Seleccione un rol.");
    }
}

public class RolCreateDtoValidator : AbstractValidator<RolCreateDto>
{
    public RolCreateDtoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre del rol es obligatorio.").Length(2, 100).WithMessage("El nombre debe tener entre 2 y 100 caracteres.");
        RuleFor(x => x.Descripcion).MaximumLength(250);
        RuleFor(x => x.Permisos).NotNull().WithMessage("Indique los permisos del rol.");
    }
}

public class RolUpdateDtoValidator : AbstractValidator<RolUpdateDto>
{
    public RolUpdateDtoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre del rol es obligatorio.").Length(2, 100).WithMessage("El nombre debe tener entre 2 y 100 caracteres.");
        RuleFor(x => x.Descripcion).MaximumLength(250);
        RuleFor(x => x.Permisos).NotNull().WithMessage("Indique los permisos del rol.");
    }
}
