-- ============================================================
-- 14_procedures_parametros.sql
-- Stored procedures de market.Parametro (ver 01_schema_and_tables.sql). Espejo de
-- database/migrations/0018_parametros.sql para una base creada desde cero.
-- ============================================================

-- Prioriza el override de la sucursal puntual (@SucursalId); si no hay fila para esa sucursal (o no
-- se pidió, @SucursalId = NULL), cae al valor de empresa (SucursalId = 0). Si tampoco hay fila,
-- devuelve NULL y el llamador (ParametroRepository) asume "habilitada" (fail-safe).
CREATE OR ALTER PROCEDURE market.usp_Parametro_ObtenerAuditoriaHabilitada
    @EmpresaId INT, @SucursalId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 AuditoriaHabilitada
    FROM market.Parametro
    WHERE EmpresaId = @EmpresaId AND SucursalId IN (@SucursalId, 0)
    ORDER BY CASE WHEN SucursalId = @SucursalId THEN 0 ELSE 1 END;
END
GO

-- Upsert de la bandera para una empresa/sucursal puntual (@SucursalId = 0 para el valor de empresa).
CREATE OR ALTER PROCEDURE market.usp_Parametro_ActualizarAuditoriaHabilitada
    @EmpresaId INT, @SucursalId INT, @AuditoriaHabilitada BIT, @ModificadoPorUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE market.Parametro AS destino
    USING (SELECT @EmpresaId AS EmpresaId, @SucursalId AS SucursalId) AS origen
        ON destino.EmpresaId = origen.EmpresaId AND destino.SucursalId = origen.SucursalId
    WHEN MATCHED THEN
        UPDATE SET AuditoriaHabilitada = @AuditoriaHabilitada,
                   ModificadoPorUsuarioId = @ModificadoPorUsuarioId,
                   FechaModificacion = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (EmpresaId, SucursalId, AuditoriaHabilitada, ModificadoPorUsuarioId, FechaModificacion)
        VALUES (@EmpresaId, @SucursalId, @AuditoriaHabilitada, @ModificadoPorUsuarioId, SYSUTCDATETIME());
END
GO
