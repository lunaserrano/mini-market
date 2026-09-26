-- ============================================================
-- 0018_parametros.sql
-- Tabla de parámetros de configuración por empresa/sucursal (llave primaria compuesta
-- EmpresaId + SucursalId). Primer parámetro: la bandera para activar/desactivar el registro de
-- auditoría (market.EventoSeguridad), que crece rápido (ver 0006_auditoria_actividad.sql) y a veces
-- conviene apagar sin tocar código ni appsettings.
--
-- SucursalId = 0 representa "toda la empresa" (no es una sucursal real de market.Sucursal): permite
-- un valor por defecto a nivel de empresa y, más adelante, overrides por sucursal puntual si hiciera
-- falta, sin cambiar el esquema.
--
-- Si no existe fila para una empresa, se asume auditoría HABILITADA (fail-safe: nunca se deja de
-- auditar por una fila faltante; hay que apagarla explícitamente).
-- ============================================================

CREATE TABLE market.Parametro (
    EmpresaId               INT             NOT NULL,
    SucursalId              INT             NOT NULL DEFAULT 0,
    AuditoriaHabilitada     BIT             NOT NULL DEFAULT 1,
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Parametro PRIMARY KEY (EmpresaId, SucursalId),
    CONSTRAINT FK_Parametro_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);
GO

-- Fila explícita (SucursalId = 0, habilitada) para cada empresa ya existente. No es estrictamente
-- necesaria (el fail-safe de arriba ya asume habilitada sin fila), pero deja el dato explícito en
-- vez de depender del comportamiento por defecto del procedure.
INSERT INTO market.Parametro (EmpresaId, SucursalId, AuditoriaHabilitada)
SELECT e.Id, 0, 1
FROM market.Empresa e
WHERE NOT EXISTS (SELECT 1 FROM market.Parametro p WHERE p.EmpresaId = e.Id AND p.SucursalId = 0);
GO

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
