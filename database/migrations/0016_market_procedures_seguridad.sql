-- ============================================================
-- 10_procedures_seguridad.sql
-- Stored procedures de market para Usuario, RolCatalogo/RolPermiso/Permiso, RefreshToken y
-- EventoSeguridad. Sustituyen el SQL embebido de UsuarioRepository, RolRepository,
-- RefreshTokenRepository, EventoSeguridadRepository, DataSeeder y PermisoCatalogSync.
-- ============================================================

-- ---------- Usuario ----------

CREATE OR ALTER PROCEDURE market.usp_Usuario_ObtenerPorUsername
    @EmpresaId INT, @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Usuario WHERE EmpresaId = @EmpresaId AND Username = @Username;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Usuario WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

-- Sin filtro de empresa: solo para resolver la sesión de un refresh token.
CREATE OR ALTER PROCEDURE market.usp_Usuario_ObtenerParaSesion
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Usuario WHERE Id = @UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_Listar
    @EmpresaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Usuario WHERE EmpresaId = @EmpresaId ORDER BY NombreCompleto;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_Crear
    @EmpresaId INT, @SucursalId INT, @RolId INT, @NombreCompleto NVARCHAR(150), @Username NVARCHAR(50),
    @PasswordHash NVARCHAR(255), @Estado CHAR(1), @DebeCambiarPassword BIT, @CreadoPorUsuarioId INT, @FechaCreacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Usuario (EmpresaId, SucursalId, RolId, NombreCompleto, Username, PasswordHash, Estado, DebeCambiarPassword, CreadoPorUsuarioId, FechaCreacion)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @SucursalId, @RolId, @NombreCompleto, @Username, @PasswordHash, @Estado, @DebeCambiarPassword, @CreadoPorUsuarioId, @FechaCreacion);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_Actualizar
    @Id INT, @EmpresaId INT, @SucursalId INT, @RolId INT, @NombreCompleto NVARCHAR(150),
    @ModificadoPorUsuarioId INT, @FechaModificacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Usuario
    SET SucursalId = @SucursalId, RolId = @RolId, NombreCompleto = @NombreCompleto,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_CambiarEstado
    @EmpresaId INT, @Id INT, @Estado CHAR(1), @ModificadoPorUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Usuario
    SET Estado = @Estado, ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = SYSUTCDATETIME()
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_ActualizarPassword
    @EmpresaId INT, @Id INT, @PasswordHash NVARCHAR(255), @DebeCambiarPassword BIT, @ModificadoPorUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Usuario
    SET PasswordHash = @PasswordHash, DebeCambiarPassword = @DebeCambiarPassword,
        PasswordCambiadaUtc = SYSUTCDATETIME(), IntentosFallidos = 0, BloqueadoHasta = NULL,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = SYSUTCDATETIME()
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

-- Una sola sentencia: el incremento y la decisión de bloquear son atómicos aunque lleguen logins
-- concurrentes. Al bloquear, el contador vuelve a 0 para que al vencer el bloqueo haya intentos nuevos.
CREATE OR ALTER PROCEDURE market.usp_Usuario_RegistrarIntentoFallido
    @EmpresaId INT, @Id INT, @MaxIntentos INT, @BloqueoHastaUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Usuario SET
        IntentosFallidos = CASE WHEN IntentosFallidos + 1 >= @MaxIntentos THEN 0 ELSE IntentosFallidos + 1 END,
        BloqueadoHasta   = CASE WHEN IntentosFallidos + 1 >= @MaxIntentos THEN @BloqueoHastaUtc ELSE NULL END
    OUTPUT INSERTED.BloqueadoHasta
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_RegistrarLoginOk
    @EmpresaId INT, @Id INT, @AhoraUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Usuario SET IntentosFallidos = 0, BloqueadoHasta = NULL, UltimoLoginUtc = @AhoraUtc
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_Desbloquear
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Usuario SET IntentosFallidos = 0, BloqueadoHasta = NULL WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Usuario_ContarAdministradoresActivos
    @EmpresaId INT, @ExcluirUsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*)
    FROM market.Usuario u
    JOIN market.RolCatalogo r ON r.Id = u.RolId
    WHERE u.EmpresaId = @EmpresaId AND u.Estado = 'A'
      AND r.EsSistema = 1 AND r.Codigo = 'admin'
      AND (@ExcluirUsuarioId IS NULL OR u.Id <> @ExcluirUsuarioId);
END
GO

-- ---------- RolCatalogo / RolPermiso / Permiso ----------

-- El admin no tiene filas en RolPermiso: su total es el del catálogo completo (@TotalCatalogo lo
-- calcula la Api desde Domain/Security/Permisos.cs, fuente de verdad del catálogo en código).
CREATE OR ALTER PROCEDURE market.usp_Rol_Listar
    @EmpresaId INT, @TotalCatalogo INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.Id, r.Codigo, r.Nombre, r.Descripcion, r.EsSistema,
           (SELECT COUNT(*) FROM market.Usuario u WHERE u.RolId = r.Id) AS TotalUsuarios,
           CASE WHEN r.EsSistema = 1 AND r.Codigo = 'admin' THEN @TotalCatalogo
                ELSE (SELECT COUNT(*) FROM market.RolPermiso rp WHERE rp.RolId = r.Id) END AS TotalPermisos
    FROM market.RolCatalogo r
    WHERE r.EmpresaId = @EmpresaId AND r.Estado = 'A'
    ORDER BY r.EsSistema DESC, r.Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Rol_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.RolCatalogo WHERE EmpresaId = @EmpresaId AND Id = @Id AND Estado = 'A';
END
GO

CREATE OR ALTER PROCEDURE market.usp_Rol_ObtenerCodigosPermisos
    @RolId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.Codigo FROM market.RolPermiso rp JOIN market.Permiso p ON p.Id = rp.PermisoId
    WHERE rp.RolId = @RolId ORDER BY p.Codigo;
END
GO

-- Crea el rol y le asigna los permisos indicados (@Permisos puede venir vacío) en una sola
-- transacción atómica. Devuelve el Id del rol creado.
CREATE OR ALTER PROCEDURE market.usp_Rol_Crear
    @EmpresaId INT, @Codigo NVARCHAR(50), @Nombre NVARCHAR(100), @Descripcion NVARCHAR(250), @EsSistema BIT,
    @Estado CHAR(1), @CreadoPorUsuarioId INT, @FechaCreacion DATETIME2, @Permisos market.CodigoListType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @RolId INT;
    INSERT INTO market.RolCatalogo (EmpresaId, Codigo, Nombre, Descripcion, EsSistema, Estado, CreadoPorUsuarioId, FechaCreacion)
    VALUES (@EmpresaId, @Codigo, @Nombre, @Descripcion, @EsSistema, @Estado, @CreadoPorUsuarioId, @FechaCreacion);
    SET @RolId = SCOPE_IDENTITY();

    INSERT INTO market.RolPermiso (RolId, PermisoId)
    SELECT @RolId, p.Id FROM market.Permiso p INNER JOIN @Permisos c ON c.Codigo = p.Codigo;

    COMMIT TRANSACTION;
    SELECT @RolId;
END
GO

-- Actualiza nombre/descripción y, si @ActualizarPermisos = 1, reemplaza el conjunto de permisos
-- (incluyendo dejarlo vacío) en la misma transacción. Sustituye a "permisos IReadOnlyCollection<string>?".
CREATE OR ALTER PROCEDURE market.usp_Rol_Actualizar
    @Id INT, @EmpresaId INT, @Nombre NVARCHAR(100), @Descripcion NVARCHAR(250),
    @ModificadoPorUsuarioId INT, @FechaModificacion DATETIME2,
    @ActualizarPermisos BIT, @Permisos market.CodigoListType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE market.RolCatalogo
    SET Nombre = @Nombre, Descripcion = @Descripcion,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
    WHERE Id = @Id AND EmpresaId = @EmpresaId;

    IF @ActualizarPermisos = 1
    BEGIN
        DELETE FROM market.RolPermiso WHERE RolId = @Id;
        INSERT INTO market.RolPermiso (RolId, PermisoId)
        SELECT @Id, p.Id FROM market.Permiso p INNER JOIN @Permisos c ON c.Codigo = p.Codigo;
    END

    COMMIT TRANSACTION;
END
GO

-- El filtro por empresa y EsSistema = 0 protege también a nivel de SQL: nunca se borra un rol de
-- sistema ni de otra empresa, aunque el llamador se equivoque.
CREATE OR ALTER PROCEDURE market.usp_Rol_Eliminar
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DELETE rp FROM market.RolPermiso rp JOIN market.RolCatalogo r ON r.Id = rp.RolId
    WHERE r.Id = @Id AND r.EmpresaId = @EmpresaId AND r.EsSistema = 0;

    DELETE FROM market.RolCatalogo WHERE Id = @Id AND EmpresaId = @EmpresaId AND EsSistema = 0;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Rol_ContarUsuarios
    @EmpresaId INT, @RolId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM market.Usuario WHERE EmpresaId = @EmpresaId AND RolId = @RolId;
END
GO

-- Sincroniza el catálogo global de permisos con el definido en código (Domain/Security/Permisos.cs):
-- agrega los nuevos y corrige nombre/módulo de los existentes en una sola sentencia MERGE por lote
-- (antes: una transacción con un MERGE por permiso, uno a uno). No borra: un permiso retirado del
-- código queda huérfano en la tabla.
CREATE OR ALTER PROCEDURE market.usp_Permiso_SincronizarLote
    @Permisos market.PermisoListType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    MERGE market.Permiso AS destino
    USING @Permisos AS origen
        ON destino.Codigo = origen.Codigo
    WHEN MATCHED AND (destino.Modulo <> origen.Modulo OR destino.Nombre <> origen.Nombre) THEN
        UPDATE SET Modulo = origen.Modulo, Nombre = origen.Nombre
    WHEN NOT MATCHED THEN
        INSERT (Codigo, Modulo, Nombre) VALUES (origen.Codigo, origen.Modulo, origen.Nombre);
END
GO

-- ---------- RefreshToken ----------

CREATE OR ALTER PROCEDURE market.usp_RefreshToken_Crear
    @UsuarioId INT, @TokenHash NVARCHAR(64), @FamiliaId UNIQUEIDENTIFIER, @CreadoUtc DATETIME2,
    @ExpiraUtc DATETIME2, @Ip NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.RefreshToken (UsuarioId, TokenHash, FamiliaId, CreadoUtc, ExpiraUtc, Ip)
    OUTPUT INSERTED.Id
    VALUES (@UsuarioId, @TokenHash, @FamiliaId, @CreadoUtc, @ExpiraUtc, @Ip);
END
GO

CREATE OR ALTER PROCEDURE market.usp_RefreshToken_ObtenerPorHash
    @TokenHash NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.RefreshToken WHERE TokenHash = @TokenHash;
END
GO

-- "AND RevocadoUtc IS NULL" hace la rotación atómica: solo una petición concurrente gana.
CREATE OR ALTER PROCEDURE market.usp_RefreshToken_Revocar
    @Id INT, @AhoraUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.RefreshToken SET RevocadoUtc = @AhoraUtc WHERE Id = @Id AND RevocadoUtc IS NULL;
    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE market.usp_RefreshToken_MarcarReemplazo
    @Id INT, @ReemplazadoPorId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.RefreshToken SET ReemplazadoPorId = @ReemplazadoPorId WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_RefreshToken_RevocarFamilia
    @FamiliaId UNIQUEIDENTIFIER, @AhoraUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.RefreshToken SET RevocadoUtc = @AhoraUtc WHERE FamiliaId = @FamiliaId AND RevocadoUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE market.usp_RefreshToken_RevocarTodosDeUsuario
    @UsuarioId INT, @AhoraUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.RefreshToken SET RevocadoUtc = @AhoraUtc WHERE UsuarioId = @UsuarioId AND RevocadoUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE market.usp_RefreshToken_PurgarAntiguos
    @UsuarioId INT, @AntesDeUtc DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM market.RefreshToken
    WHERE UsuarioId = @UsuarioId AND (ExpiraUtc < @AntesDeUtc OR RevocadoUtc < @AntesDeUtc);
END
GO

-- ---------- EventoSeguridad ----------

CREATE OR ALTER PROCEDURE market.usp_EventoSeguridad_Registrar
    @EmpresaId INT, @ActorUsuarioId INT, @UsuarioObjetivoId INT, @Tipo NVARCHAR(40), @Detalle NVARCHAR(500),
    @Ip NVARCHAR(45), @UserAgent NVARCHAR(250), @FechaUtc DATETIME2, @Origen VARCHAR(3), @Metodo VARCHAR(10),
    @Ruta NVARCHAR(300), @StatusCode SMALLINT, @DuracionMs INT, @Datos NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.EventoSeguridad (EmpresaId, ActorUsuarioId, UsuarioObjetivoId, Tipo, Detalle, Ip, UserAgent, FechaUtc,
                                 Origen, Metodo, Ruta, StatusCode, DuracionMs, Datos)
    VALUES (@EmpresaId, @ActorUsuarioId, @UsuarioObjetivoId, @Tipo, @Detalle, @Ip, @UserAgent, @FechaUtc,
            @Origen, @Metodo, @Ruta, @StatusCode, @DuracionMs, @Datos);
END
GO

-- Inserta varios eventos en una sola sentencia/transacción (usado por el escritor en segundo plano
-- de la auditoría, AuditoriaWriterService, para no ida-y-vuelta uno por uno).
CREATE OR ALTER PROCEDURE market.usp_EventoSeguridad_RegistrarLote
    @Eventos market.EventoSeguridadListType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.EventoSeguridad (EmpresaId, ActorUsuarioId, UsuarioObjetivoId, Tipo, Detalle, Ip, UserAgent, FechaUtc,
                                 Origen, Metodo, Ruta, StatusCode, DuracionMs, Datos)
    SELECT EmpresaId, ActorUsuarioId, UsuarioObjetivoId, Tipo, Detalle, Ip, UserAgent, FechaUtc,
           Origen, Metodo, Ruta, StatusCode, DuracionMs, Datos
    FROM @Eventos;
END
GO

-- Result set 1: total de filas que matchean el filtro (para paginar). Result set 2: la página pedida.
CREATE OR ALTER PROCEDURE market.usp_EventoSeguridad_Listar
    @EmpresaId INT, @Desde DATETIME2 = NULL, @Hasta DATETIME2 = NULL, @UsuarioId INT = NULL,
    @Tipo NVARCHAR(40) = NULL, @Origen VARCHAR(3) = NULL, @Offset INT, @TamanoPagina INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*)
    FROM market.EventoSeguridad e
    WHERE e.EmpresaId = @EmpresaId
      AND (@Desde IS NULL OR e.FechaUtc >= @Desde)
      AND (@Hasta IS NULL OR e.FechaUtc <= @Hasta)
      AND (@UsuarioId IS NULL OR e.ActorUsuarioId = @UsuarioId OR e.UsuarioObjetivoId = @UsuarioId)
      AND (@Tipo IS NULL OR e.Tipo = @Tipo)
      AND (@Origen IS NULL OR e.Origen = @Origen);

    SELECT e.Id, e.FechaUtc, e.Tipo, e.Detalle, e.ActorUsuarioId, ua.NombreCompleto AS ActorNombre,
           e.UsuarioObjetivoId, uo.NombreCompleto AS ObjetivoNombre, e.Ip,
           e.Origen, e.Metodo, e.Ruta, e.StatusCode, e.DuracionMs, e.Datos
    FROM market.EventoSeguridad e
    LEFT JOIN market.Usuario ua ON ua.Id = e.ActorUsuarioId
    LEFT JOIN market.Usuario uo ON uo.Id = e.UsuarioObjetivoId
    WHERE e.EmpresaId = @EmpresaId
      AND (@Desde IS NULL OR e.FechaUtc >= @Desde)
      AND (@Hasta IS NULL OR e.FechaUtc <= @Hasta)
      AND (@UsuarioId IS NULL OR e.ActorUsuarioId = @UsuarioId OR e.UsuarioObjetivoId = @UsuarioId)
      AND (@Tipo IS NULL OR e.Tipo = @Tipo)
      AND (@Origen IS NULL OR e.Origen = @Origen)
    ORDER BY e.FechaUtc DESC, e.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO
