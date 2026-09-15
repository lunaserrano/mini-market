namespace MiniMarket.Domain.Entities;

public class Caja
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int UsuarioAperturaId { get; set; }
    public DateTime FechaApertura { get; set; }
    public decimal MontoInicial { get; set; }
    public int? UsuarioCierreId { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? MontoFinalDeclarado { get; set; }
    /// <summary>Calculado por el backend al cerrar: MontoInicial + ingresos (incl. ventas en efectivo) - egresos.</summary>
    public decimal? MontoFinalSistema { get; set; }
    public decimal? Diferencia { get; set; }
    /// <summary>"ABIERTA" | "CERRADA", validado con CHECK constraint.</summary>
    public string Estado { get; set; } = "ABIERTA";
}

/// <summary>
/// Tabla nueva (fix #4): el script original solo tenía apertura/cierre de Caja, sin forma de
/// registrar ingresos y egresos manuales (retiros, depósitos, gastos) durante el turno,
/// pese a que el requerimiento 5 lo pide explícitamente.
/// </summary>
public class MovimientoCaja
{
    public int Id { get; set; }
    public int CajaId { get; set; }
    /// <summary>"INGRESO" | "EGRESO", validado con CHECK constraint.</summary>
    public string Tipo { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int UsuarioId { get; set; }
    public DateTime Fecha { get; set; }
    /// <summary>Referencia opcional (ej. a una Venta en efectivo) para que el corte de caja cuadre automáticamente.</summary>
    public string? DocumentoReferenciaTipo { get; set; }
    public int? DocumentoReferenciaId { get; set; }
}
