-- ============================================================
-- 02_types.sql
-- Tipos de tabla (Table-Valued Parameters) usados por los stored procedures de market para recibir
-- listas desde la Api (Dapper los pasa con SqlMapper.AsTableValuedParameter), evitando concatenar
-- SQL dinámico para IN (...) con listas de tamaño variable.
-- ============================================================

CREATE TYPE market.CodigoListType AS TABLE (
    Codigo NVARCHAR(60) NOT NULL PRIMARY KEY
);
GO

CREATE TYPE market.PermisoListType AS TABLE (
    Codigo  NVARCHAR(60)    NOT NULL PRIMARY KEY,
    Modulo  NVARCHAR(40)    NOT NULL,
    Nombre  NVARCHAR(100)   NOT NULL
);
GO

CREATE TYPE market.EventoSeguridadListType AS TABLE (
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
);
GO
