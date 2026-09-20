-- ============================================================
-- 0006_auditoria_actividad.sql
-- Amplía EventoSeguridad para que la auditoría cubra TODA la actividad del sistema, no solo los
-- eventos de seguridad:
--
--  * Origen 'SEG' : eventos de seguridad de siempre (login, roles, usuarios...).
--  * Origen 'API' : cada petición HTTP atendida por el backend (la registra AuditoriaMiddleware).
--  * Origen 'UI'  : clics y navegación del frontend (los envía el navegador por lotes).
--
-- La tabla conserva el nombre EventoSeguridad para no romper repositorios ni consultas existentes.
-- ============================================================

ALTER TABLE EventoSeguridad ADD
    Origen      VARCHAR(3)      NOT NULL CONSTRAINT DF_EventoSeguridad_Origen DEFAULT 'SEG',
    Metodo      VARCHAR(10)     NULL,
    Ruta        NVARCHAR(300)   NULL,
    StatusCode  SMALLINT        NULL,
    DuracionMs  INT             NULL,
    -- JSON con el cuerpo de la petición (sin secretos) o el descriptor del elemento clicado.
    Datos       NVARCHAR(MAX)   NULL;
GO

CREATE INDEX IX_EventoSeguridad_Empresa_Origen_Fecha ON EventoSeguridad(EmpresaId, Origen, FechaUtc DESC);
CREATE INDEX IX_EventoSeguridad_Actor_Fecha ON EventoSeguridad(ActorUsuarioId, FechaUtc DESC);
GO
