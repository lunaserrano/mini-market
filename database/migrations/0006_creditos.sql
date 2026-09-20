-- ============================================================
-- 0006_creditos.sql
-- Ventas a crédito: el cliente se lleva la mercadería sin pagar (o pagando solo una parte) y
-- va abonando hasta cancelar la deuda.
--
--  * Credito: una fila por venta a crédito (1:1 con Venta). MontoOriginal = Total de la venta
--    menos lo pagado en el momento (PagoVenta); SaldoPendiente baja con cada abono.
--  * AbonoCredito: cada pago posterior. Es inmutable (no se edita ni se borra); si el abono
--    fue en efectivo se liga a la Caja abierta que lo recibió, y además genera un ingreso en
--    MovimientoCaja para que el corte cuadre.
--  * Estado: PENDIENTE (con saldo) -> PAGADO (saldo 0) | ANULADO (la venta origen se anuló).
-- ============================================================

CREATE TABLE Credito (
    Id                  INT IDENTITY PRIMARY KEY,
    EmpresaId           INT             NOT NULL,
    VentaId             INT             NOT NULL,
    ClienteId           INT             NOT NULL,
    MontoOriginal       DECIMAL(18,2)   NOT NULL CHECK (MontoOriginal > 0),
    SaldoPendiente      DECIMAL(18,2)   NOT NULL CHECK (SaldoPendiente >= 0),
    Estado              NVARCHAR(20)    NOT NULL DEFAULT 'PENDIENTE' CHECK (Estado IN ('PENDIENTE','PAGADO','ANULADO')),
    FechaVencimiento    DATETIME2       NULL,
    FechaCreacion       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CreadoPorUsuarioId  INT             NOT NULL,
    FechaCancelacion    DATETIME2       NULL,
    CONSTRAINT FK_Credito_Empresa  FOREIGN KEY (EmpresaId) REFERENCES Empresa(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Credito_Venta    FOREIGN KEY (VentaId)   REFERENCES Venta(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Credito_Cliente  FOREIGN KEY (ClienteId) REFERENCES Cliente(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Credito_Usuario  FOREIGN KEY (CreadoPorUsuarioId) REFERENCES Usuario(Id),
    CONSTRAINT CK_Credito_Saldo    CHECK (SaldoPendiente <= MontoOriginal)
);
CREATE UNIQUE INDEX UX_Credito_VentaId ON Credito(VentaId);
CREATE INDEX IX_Credito_Empresa_Estado ON Credito(EmpresaId, Estado);
CREATE INDEX IX_Credito_ClienteId ON Credito(ClienteId);

CREATE TABLE AbonoCredito (
    Id          INT IDENTITY PRIMARY KEY,
    CreditoId   INT             NOT NULL,
    CajaId      INT             NULL,
    UsuarioId   INT             NOT NULL,
    Metodo      NVARCHAR(20)    NOT NULL CHECK (Metodo IN ('EFECTIVO','TARJETA','TRANSFERENCIA')),
    Monto       DECIMAL(18,2)   NOT NULL CHECK (Monto > 0),
    Referencia  NVARCHAR(100)   NULL,
    Fecha       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AbonoCredito_Credito FOREIGN KEY (CreditoId) REFERENCES Credito(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_AbonoCredito_Caja    FOREIGN KEY (CajaId)    REFERENCES Caja(Id),
    CONSTRAINT FK_AbonoCredito_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
);
CREATE INDEX IX_AbonoCredito_CreditoId ON AbonoCredito(CreditoId);
GO

-- ------------------------------------------------------------
-- Permisos. PermisoCatalogSync completa Nombre/Descripcion al arrancar; aquí se siembran los
-- códigos y se asignan a los roles de sistema de las empresas existentes (las nuevas los
-- reciben del DataSeeder vía Permisos.PorDefecto). El admin no lleva filas: tiene todo.
-- ------------------------------------------------------------
INSERT INTO Permiso (Codigo, Modulo, Nombre)
SELECT v.Codigo, LEFT(v.Codigo, CHARINDEX('.', v.Codigo) - 1), v.Codigo
FROM (VALUES ('creditos.ver'), ('creditos.abonar'), ('creditos.otorgar')) AS v(Codigo)
WHERE NOT EXISTS (SELECT 1 FROM Permiso p WHERE p.Codigo = v.Codigo);
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM RolCatalogo r
JOIN (VALUES
    ('supervisor','creditos.ver'), ('supervisor','creditos.abonar'), ('supervisor','creditos.otorgar'),
    ('cajero','creditos.ver'),     ('cajero','creditos.abonar'),     ('cajero','creditos.otorgar')
) AS d(RolCodigo, PermisoCodigo) ON d.RolCodigo = r.Codigo
JOIN Permiso p ON p.Codigo = d.PermisoCodigo
WHERE r.EsSistema = 1
  AND NOT EXISTS (SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id);
GO
