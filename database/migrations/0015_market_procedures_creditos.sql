-- ============================================================
-- 09_procedures_creditos.sql
-- Stored procedures de market para Credito y AbonoCredito.
-- Sustituyen el SQL embebido de CreditoRepository (Infrastructure/Persistence).
-- ============================================================

CREATE OR ALTER PROCEDURE market.usp_Credito_Crear
    @EmpresaId INT, @VentaId INT, @ClienteId INT, @MontoOriginal DECIMAL(18,2), @SaldoPendiente DECIMAL(18,2),
    @Estado NVARCHAR(20), @FechaVencimiento DATETIME2, @FechaCreacion DATETIME2, @CreadoPorUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Credito (EmpresaId, VentaId, ClienteId, MontoOriginal, SaldoPendiente, Estado,
        FechaVencimiento, FechaCreacion, CreadoPorUsuarioId)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @VentaId, @ClienteId, @MontoOriginal, @SaldoPendiente, @Estado,
        @FechaVencimiento, @FechaCreacion, @CreadoPorUsuarioId);
END
GO

-- UPDLOCK+ROWLOCK: dos abonos simultáneos no pueden pisarse el saldo. Debe ejecutarse dentro de la
-- transacción activa del abono/anulación (misma conexión).
CREATE OR ALTER PROCEDURE market.usp_Credito_ObtenerParaActualizar
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Credito WITH (UPDLOCK, ROWLOCK) WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Credito_ObtenerPorVenta
    @VentaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Credito WITH (UPDLOCK, ROWLOCK) WHERE VentaId = @VentaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_AbonoCredito_Contar
    @CreditoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM market.AbonoCredito WHERE CreditoId = @CreditoId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_AbonoCredito_Registrar
    @CreditoId INT, @CajaId INT, @UsuarioId INT, @Metodo NVARCHAR(20), @Monto DECIMAL(18,2),
    @Referencia NVARCHAR(100), @Fecha DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.AbonoCredito (CreditoId, CajaId, UsuarioId, Metodo, Monto, Referencia, Fecha)
    VALUES (@CreditoId, @CajaId, @UsuarioId, @Metodo, @Monto, @Referencia, @Fecha);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Credito_ActualizarSaldo
    @Id INT, @SaldoPendiente DECIMAL(18,2), @Estado NVARCHAR(20), @FechaCancelacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Credito SET SaldoPendiente = @SaldoPendiente, Estado = @Estado, FechaCancelacion = @FechaCancelacion
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Credito_Anular
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Credito SET Estado = 'ANULADO' WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Credito_Listar
    @EmpresaId INT, @ClienteId INT = NULL, @Estado NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT cr.Id, cr.VentaId, v.Folio AS VentaFolio, cr.ClienteId, c.Nombre AS ClienteNombre,
           cr.FechaCreacion, cr.FechaVencimiento, cr.MontoOriginal, cr.SaldoPendiente, cr.Estado,
           CAST(CASE WHEN cr.Estado = 'PENDIENTE' AND cr.FechaVencimiento IS NOT NULL AND cr.FechaVencimiento < SYSUTCDATETIME() THEN 1 ELSE 0 END AS BIT) AS Vencido
    FROM market.Credito cr
    INNER JOIN market.Venta v ON v.Id = cr.VentaId
    INNER JOIN market.Cliente c ON c.Id = cr.ClienteId
    WHERE cr.EmpresaId = @EmpresaId
      AND (@ClienteId IS NULL OR cr.ClienteId = @ClienteId)
      AND (@Estado IS NULL OR cr.Estado = @Estado)
    ORDER BY CASE WHEN cr.Estado = 'PENDIENTE' THEN 0 ELSE 1 END, cr.FechaCreacion DESC;
END
GO

-- Result set 1: cabecera del crédito. Result set 2: abonos.
CREATE OR ALTER PROCEDURE market.usp_Credito_ObtenerDetalle
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT cr.Id, cr.VentaId, v.Folio AS VentaFolio, cr.ClienteId, c.Nombre AS ClienteNombre,
           cr.FechaCreacion, cr.FechaVencimiento, cr.FechaCancelacion,
           v.Total AS TotalVenta, cr.MontoOriginal, cr.SaldoPendiente, cr.Estado,
           CAST(CASE WHEN cr.Estado = 'PENDIENTE' AND cr.FechaVencimiento IS NOT NULL AND cr.FechaVencimiento < SYSUTCDATETIME() THEN 1 ELSE 0 END AS BIT) AS Vencido
    FROM market.Credito cr
    INNER JOIN market.Venta v ON v.Id = cr.VentaId
    INNER JOIN market.Cliente c ON c.Id = cr.ClienteId
    WHERE cr.EmpresaId = @EmpresaId AND cr.Id = @Id;

    SELECT a.Id, a.Fecha, a.Metodo, a.Monto, a.Referencia, u.NombreCompleto AS UsuarioNombre
    FROM market.AbonoCredito a
    INNER JOIN market.Usuario u ON u.Id = a.UsuarioId
    WHERE a.CreditoId = @Id
    ORDER BY a.Fecha DESC, a.Id DESC;
END
GO
