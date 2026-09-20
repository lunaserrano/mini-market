using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Api.Filters;

/// <summary>
/// Ejecuta el IValidator&lt;T&gt; registrado (si existe) de cada argumento de la acción antes de invocarla.
/// Si hay errores lanza <see cref="ValidacionException"/>, que ExceptionHandlingMiddleware traduce a 400.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errores = new Dictionary<string, List<string>>();

        foreach (var argumento in context.ActionArguments.Values)
        {
            if (argumento is null) continue;
            if (_services.GetService(typeof(IValidator<>).MakeGenericType(argumento.GetType())) is not IValidator validador) continue;

            var resultado = await validador.ValidateAsync(new ValidationContext<object>(argumento), context.HttpContext.RequestAborted);
            foreach (var error in resultado.Errors)
            {
                if (!errores.TryGetValue(error.PropertyName, out var lista))
                    errores[error.PropertyName] = lista = new List<string>();
                lista.Add(error.ErrorMessage);
            }
        }

        if (errores.Count > 0)
            throw new ValidacionException(errores.ToDictionary(e => e.Key, e => e.Value.ToArray()));

        await next();
    }
}
