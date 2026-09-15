namespace MiniMarket.Domain.Enums;

/// <summary>Estado de un registro maestro/catálogo (soft delete). Se persiste como CHAR(1) 'A'/'I'.</summary>
public enum EstadoRegistro
{
    Activo,
    Inactivo
}

/// <summary>Roles disponibles. El código (string) es la fuente de verdad en BD (tabla RolCatalogo);
/// este enum es una conveniencia fuertemente tipada para el código de aplicación.</summary>
public enum RolUsuario
{
    Admin,
    Supervisor,
    Cajero
}

public enum EstadoCaja
{
    Abierta,
    Cerrada
}

public enum TipoMovimientoCaja
{
    Ingreso,
    Egreso
}

/// <summary>Tipo de movimiento de inventario. Persistido como texto corto validado con CHECK constraint en BD.</summary>
public enum TipoMovimientoInventario
{
    EntradaCompra,
    SalidaVenta,
    AjustePositivo,
    AjusteNegativo,
    TrasladoEntrada,
    TrasladoSalida,
    DevolucionVenta,
    DevolucionCompra
}

public enum MetodoPago
{
    Efectivo,
    Tarjeta,
    Transferencia
}

/// <summary>Estado de un documento transaccional (Venta/Compra). Nunca se borra, solo se anula.</summary>
public enum EstadoDocumento
{
    Completada,
    Anulada
}

/// <summary>Tipo de documento origen de un MovimientoInventario, para trazabilidad estructurada
/// (reemplaza el campo de texto libre "referencia" del modelo original).</summary>
public enum DocumentoOrigenTipo
{
    Venta,
    Compra,
    AjusteManual,
    Traslado
}
