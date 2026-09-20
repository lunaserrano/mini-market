using MiniMarket.Application.DTOs;
using MiniMarket.Application.Validators;

namespace MiniMarket.Tests;

public class ValidatorTests
{
    [Theory]
    [InlineData("Abcdef1!")]
    [InlineData("Una-Clave-Larga_2026")]
    [InlineData("ñandú.Ñ4ndu")]
    public void PoliticaDeContrasenas_AceptaClavesQueCumplenTodasLasReglas(string clave)
    {
        Assert.True(new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(clave)).IsValid);
    }

    [Theory]
    [InlineData("", "obligatoria")]
    [InlineData("Ab1!", "8 caracteres")]
    [InlineData("abcdefg1!", "mayúscula")]
    [InlineData("ABCDEFG1!", "minúscula")]
    [InlineData("Abcdefgh!", "número")]
    [InlineData("Abcdefg12", "símbolo")]
    public void PoliticaDeContrasenas_RechazaClavesQueIncumplenUnaRegla(string clave, string fragmentoDelMensaje)
    {
        var resultado = new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(clave));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains(fragmentoDelMensaje, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PoliticaDeContrasenas_RechazaClavesExcesivamenteLargas()
    {
        var clave = "Aa1!" + new string('x', PasswordPolicy.LongitudMaxima);

        Assert.False(new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(clave)).IsValid);
    }

    [Fact]
    public void CambiarPassword_ExigeLaActualYAplicaLaPoliticaALaNueva()
    {
        var validator = new CambiarPasswordRequestValidator();

        Assert.True(validator.Validate(new CambiarPasswordRequest("cualquiera", "NuevaClave1!")).IsValid);
        Assert.False(validator.Validate(new CambiarPasswordRequest("", "NuevaClave1!")).IsValid);
        Assert.False(validator.Validate(new CambiarPasswordRequest("cualquiera", "debil")).IsValid);
    }

    [Fact]
    public void Login_NoAplicaLaPoliticaDeContrasenas_SoloExigeQueVengaAlgo()
    {
        var validator = new LoginRequestValidator();

        Assert.True(validator.Validate(new LoginRequest("admin", "x")).IsValid); // cuentas antiguas con claves que hoy serían débiles
        Assert.False(validator.Validate(new LoginRequest("", "x")).IsValid);
        Assert.False(validator.Validate(new LoginRequest("admin", "")).IsValid);
    }

    [Theory]
    [InlineData("ana", true)]
    [InlineData("ana.gomez-2", true)]
    [InlineData("an", false)]
    [InlineData("ana gomez", false)]
    [InlineData("ana@correo", false)]
    [InlineData("ana'; DROP TABLE Usuario;--", false)]
    public void UsuarioCreate_ValidaElFormatoDelUsername(string username, bool valido)
    {
        var dto = new UsuarioCreateDto(null, "Ana", username, "Clave123!", 2);

        Assert.Equal(valido, new UsuarioCreateDtoValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void UsuarioCreate_ExigeRolYContrasenaSegura()
    {
        var validator = new UsuarioCreateDtoValidator();

        Assert.False(validator.Validate(new UsuarioCreateDto(null, "Ana", "ana", "Clave123!", RolId: 0)).IsValid);
        Assert.False(validator.Validate(new UsuarioCreateDto(null, "Ana", "ana", "clave", RolId: 2)).IsValid);
        Assert.False(validator.Validate(new UsuarioCreateDto(null, "", "ana", "Clave123!", RolId: 2)).IsValid);
    }

    [Fact]
    public void Rol_ExigeNombreConLongitudValidaYPermisos()
    {
        var validator = new RolCreateDtoValidator();

        Assert.True(validator.Validate(new RolCreateDto("Bodeguero", null, Array.Empty<string>())).IsValid);
        Assert.False(validator.Validate(new RolCreateDto("B", null, Array.Empty<string>())).IsValid);
        Assert.False(validator.Validate(new RolCreateDto("Bodeguero", new string('d', 251), Array.Empty<string>())).IsValid);
        Assert.False(validator.Validate(new RolCreateDto("Bodeguero", null, null!)).IsValid);
    }
}
