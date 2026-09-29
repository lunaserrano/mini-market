using MiniMarket.Api;

// Api como proceso propio (Azure / desarrollo). En la instalación de escritorio la Api NO corre así:
// MiniMarket.Desktop la hospeda dentro de su propio proceso con ApiHost (ver EmbeddedApi en el cliente).
var builder = ApiHost.CrearBuilder(new WebApplicationOptions { Args = args });

var app = builder.Build();
await ApiHost.InicializarBaseDatosAsync(app);
ApiHost.ConfigurarPipeline(app);
app.Run();

// Visible para WebApplicationFactory en pruebas de integración.
public partial class Program;
