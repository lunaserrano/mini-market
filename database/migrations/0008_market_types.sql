-- ============================================================
-- 0008_market_types.sql
-- Tipos de tabla (Table-Valued Parameters) que usan los stored procedures de market para recibir
-- listas desde la Api sin concatenar SQL dinámico. Ver database/schema/market/02_types.sql (mismo
-- contenido, para una base creada desde cero).
-- ============================================================

IF TYPE_ID(N'market.CodigoListType') IS NULL
    EXEC('CREATE TYPE market.CodigoListType AS TABLE (Codigo NVARCHAR(60) NOT NULL PRIMARY KEY)');
GO

IF TYPE_ID(N'market.PermisoListType') IS NULL
    EXEC('CREATE TYPE market.PermisoListType AS TABLE (
            Codigo  NVARCHAR(60)    NOT NULL PRIMARY KEY,
            Modulo  NVARCHAR(40)    NOT NULL,
            Nombre  NVARCHAR(100)   NOT NULL
        )');
GO

IF TYPE_ID(N'market.EventoSeguridadListType') IS NULL
    EXEC('CREATE TYPE market.EventoSeguridadListType AS TABLE (
            EmpresaId           INT             NULL,
            ActorUsuarioId       INT             NULL,
            UsuarioObjetivoId    INT             NULL,
            Tipo                 NVARCHAR(40)    NOT NULL,
            Detalle               NVARCHAR(500)   NULL,
            Ip                    NVARCHAR(45)    NULL,
            UserAgent             NVARCHAR(250)   NULL,
            FechaUtc              DATETIME2       NOT NULL,
            Origen                VARCHAR(3)      NOT NULL,
            Metodo                VARCHAR(10)     NULL,
            Ruta                  NVARCHAR(300)   NULL,
            StatusCode            SMALLINT        NULL,
            DuracionMs            INT             NULL,
            Datos                 NVARCHAR(MAX)   NULL
        )');
GO
