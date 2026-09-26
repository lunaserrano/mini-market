-- ============================================================
-- 06_procedures_caja.sql
-- Stored procedures de market para Caja y MovimientoCaja.
-- Sustituyen el SQL embebido de CajaRepository (Infrastructure/Persistence).
-- ============================================================

CREATE OR ALTER PROCEDURE market.usp_Caja_ObtenerAbiertaPorUsuario
    @EmpresaId INT, @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Caja WHERE EmpresaId = @EmpresaId AND UsuarioAperturaId = @UsuarioId AND Estado = 'ABIERTA';
END
GO

CREATE OR ALTER PROCEDURE market.usp_Caja_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Caja WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Caja_Listar
    @EmpresaId INT, @SucursalId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id, c.SucursalId, c.UsuarioAperturaId, ua.NombreCompleto AS UsuarioAperturaNombre,
           c.FechaApertura, c.MontoInicial, c.UsuarioCierreId, uc.NombreCompleto AS UsuarioCierreNombre,
           c.FechaCierre, c.MontoFinalDeclarado, c.MontoFinalSistema, c.Diferencia, c.Estado
    FROM market.Caja c
    INNER JOIN market.Usuario ua ON ua.Id = c.UsuarioAperturaId
    LEFT JOIN market.Usuario uc ON uc.Id = c.UsuarioCierreId
    WHERE c.EmpresaId = @EmpresaId AND (@SucursalId IS NULL OR c.SucursalId = @SucursalId)
    ORDER BY c.FechaApertura DESC;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Caja_Abrir
    @EmpresaId INT, @SucursalId INT, @UsuarioAperturaId INT, @FechaApertura DATETIME2, @MontoInicial DECIMAL(18,2), @Estado NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Caja (EmpresaId, SucursalId, UsuarioAperturaId, FechaApertura, MontoInicial, Estado)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @SucursalId, @UsuarioAperturaId, @FechaApertura, @MontoInicial, @Estado);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Caja_Cerrar
    @Id INT, @UsuarioCierreId INT, @FechaCierre DATETIME2, @MontoFinalDeclarado DECIMAL(18,2),
    @MontoFinalSistema DECIMAL(18,2), @Diferencia DECIMAL(18,2), @Estado NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Caja
    SET UsuarioCierreId = @UsuarioCierreId, FechaCierre = @FechaCierre,
        MontoFinalDeclarado = @MontoFinalDeclarado, MontoFinalSistema = @MontoFinalSistema,
        Diferencia = @Diferencia, Estado = @Estado
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_MovimientoCaja_Registrar
    @CajaId INT, @Tipo NVARCHAR(10), @Concepto NVARCHAR(200), @Monto DECIMAL(18,2), @UsuarioId INT,
    @Fecha DATETIME2, @DocumentoReferenciaTipo NVARCHAR(20), @DocumentoReferenciaId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.MovimientoCaja (CajaId, Tipo, Concepto, Monto, UsuarioId, Fecha, DocumentoReferenciaTipo, DocumentoReferenciaId)
    OUTPUT INSERTED.Id
    VALUES (@CajaId, @Tipo, @Concepto, @Monto, @UsuarioId, @Fecha, @DocumentoReferenciaTipo, @DocumentoReferenciaId);
END
GO

CREATE OR ALTER PROCEDURE market.usp_MovimientoCaja_Listar
    @CajaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.MovimientoCaja WHERE CajaId = @CajaId ORDER BY Fecha;
END
GO

CREATE OR ALTER PROCEDURE market.usp_MovimientoCaja_ObtenerTotales
    @CajaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ISNULL(SUM(CASE WHEN Tipo = 'INGRESO' THEN Monto ELSE 0 END), 0) AS Ingresos,
        ISNULL(SUM(CASE WHEN Tipo = 'EGRESO' THEN Monto ELSE 0 END), 0) AS Egresos
    FROM market.MovimientoCaja WHERE CajaId = @CajaId;
END
GO
