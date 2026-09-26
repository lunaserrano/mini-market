-- ============================================================
-- 03_procedures_catalogos.sql
-- Stored procedures de market para Empresa, Sucursal, Categoria, Proveedor y Cliente.
-- Sustituyen el SQL embebido de EmpresaRepository y CatalogoRepositories (Infrastructure/Persistence).
-- ============================================================

-- ---------- Empresa ----------

CREATE OR ALTER PROCEDURE market.usp_Empresa_ObtenerPorId
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Empresa WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Empresa_Actualizar
    @Id INT, @Nombre NVARCHAR(150), @RazonSocial NVARCHAR(200), @IdentificacionFiscal NVARCHAR(50),
    @ZonaHoraria NVARCHAR(60), @CodigoMoneda NVARCHAR(3), @SimboloMoneda NVARCHAR(5), @TasaImpuesto DECIMAL(5,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Empresa
    SET Nombre = @Nombre, RazonSocial = @RazonSocial, IdentificacionFiscal = @IdentificacionFiscal,
        ZonaHoraria = @ZonaHoraria, CodigoMoneda = @CodigoMoneda, SimboloMoneda = @SimboloMoneda, TasaImpuesto = @TasaImpuesto
    WHERE Id = @Id;
END
GO

-- Solo usado por el seed de desarrollo (DataSeeder): crea la empresa demo inicial.
CREATE OR ALTER PROCEDURE market.usp_Empresa_Crear
    @Nombre NVARCHAR(150), @RazonSocial NVARCHAR(200), @ZonaHoraria NVARCHAR(60), @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Empresa (Nombre, RazonSocial, ZonaHoraria, Estado)
    OUTPUT INSERTED.Id
    VALUES (@Nombre, @RazonSocial, @ZonaHoraria, @Estado);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Empresa_ContarTodas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM market.Empresa;
END
GO

-- ---------- Sucursal ----------

CREATE OR ALTER PROCEDURE market.usp_Sucursal_Crear
    @EmpresaId INT, @Nombre NVARCHAR(150), @Direccion NVARCHAR(250), @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Sucursal (EmpresaId, Nombre, Direccion, Estado)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @Nombre, @Direccion, @Estado);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Sucursal_Existe
    @EmpresaId INT, @SucursalId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM market.Sucursal WHERE Id = @SucursalId AND EmpresaId = @EmpresaId;
END
GO

-- ---------- Categoria ----------

CREATE OR ALTER PROCEDURE market.usp_Categoria_Listar
    @EmpresaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Categoria WHERE EmpresaId = @EmpresaId ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Categoria_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Categoria WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Categoria_Crear
    @EmpresaId INT, @Nombre NVARCHAR(100), @Descripcion NVARCHAR(250), @Estado CHAR(1),
    @CreadoPorUsuarioId INT, @FechaCreacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Categoria (EmpresaId, Nombre, Descripcion, Estado, CreadoPorUsuarioId, FechaCreacion)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @Nombre, @Descripcion, @Estado, @CreadoPorUsuarioId, @FechaCreacion);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Categoria_Actualizar
    @Id INT, @EmpresaId INT, @Nombre NVARCHAR(100), @Descripcion NVARCHAR(250),
    @ModificadoPorUsuarioId INT, @FechaModificacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Categoria
    SET Nombre = @Nombre, Descripcion = @Descripcion,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Categoria_CambiarEstado
    @EmpresaId INT, @Id INT, @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Categoria SET Estado = @Estado WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

-- ---------- Proveedor ----------

CREATE OR ALTER PROCEDURE market.usp_Proveedor_Listar
    @EmpresaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Proveedor WHERE EmpresaId = @EmpresaId ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Proveedor_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Proveedor WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Proveedor_Crear
    @EmpresaId INT, @Nombre NVARCHAR(150), @Contacto NVARCHAR(100), @Telefono NVARCHAR(50),
    @Email NVARCHAR(150), @Direccion NVARCHAR(250), @IdentificacionFiscal NVARCHAR(50), @Estado CHAR(1),
    @CreadoPorUsuarioId INT, @FechaCreacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Proveedor (EmpresaId, Nombre, Contacto, Telefono, Email, Direccion, IdentificacionFiscal, Estado, CreadoPorUsuarioId, FechaCreacion)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @Nombre, @Contacto, @Telefono, @Email, @Direccion, @IdentificacionFiscal, @Estado, @CreadoPorUsuarioId, @FechaCreacion);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Proveedor_Actualizar
    @Id INT, @EmpresaId INT, @Nombre NVARCHAR(150), @Contacto NVARCHAR(100), @Telefono NVARCHAR(50),
    @Email NVARCHAR(150), @Direccion NVARCHAR(250), @IdentificacionFiscal NVARCHAR(50),
    @ModificadoPorUsuarioId INT, @FechaModificacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Proveedor
    SET Nombre = @Nombre, Contacto = @Contacto, Telefono = @Telefono, Email = @Email,
        Direccion = @Direccion, IdentificacionFiscal = @IdentificacionFiscal,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Proveedor_CambiarEstado
    @EmpresaId INT, @Id INT, @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Proveedor SET Estado = @Estado WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

-- ---------- Cliente ----------

CREATE OR ALTER PROCEDURE market.usp_Cliente_Listar
    @EmpresaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Cliente WHERE EmpresaId = @EmpresaId ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Cliente_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Cliente WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Cliente_Crear
    @EmpresaId INT, @Nombre NVARCHAR(150), @IdentificacionFiscal NVARCHAR(50), @Telefono NVARCHAR(50),
    @Email NVARCHAR(150), @Direccion NVARCHAR(250), @Estado CHAR(1),
    @CreadoPorUsuarioId INT, @FechaCreacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Cliente (EmpresaId, Nombre, IdentificacionFiscal, Telefono, Email, Direccion, Estado, CreadoPorUsuarioId, FechaCreacion)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @Nombre, @IdentificacionFiscal, @Telefono, @Email, @Direccion, @Estado, @CreadoPorUsuarioId, @FechaCreacion);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Cliente_Actualizar
    @Id INT, @EmpresaId INT, @Nombre NVARCHAR(150), @IdentificacionFiscal NVARCHAR(50), @Telefono NVARCHAR(50),
    @Email NVARCHAR(150), @Direccion NVARCHAR(250), @ModificadoPorUsuarioId INT, @FechaModificacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Cliente
    SET Nombre = @Nombre, IdentificacionFiscal = @IdentificacionFiscal, Telefono = @Telefono,
        Email = @Email, Direccion = @Direccion,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Cliente_CambiarEstado
    @EmpresaId INT, @Id INT, @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Cliente SET Estado = @Estado WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO
