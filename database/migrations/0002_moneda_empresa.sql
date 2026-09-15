-- ============================================================
-- 0002_moneda_empresa.sql
-- Hace la moneda configurable por empresa (mantenimiento en Configuración), en vez de asumir
-- una sola moneda fija en el código. Default: USD / "$" (El Salvador usa dólar estadounidense
-- como moneda oficial), pero cualquier empresa puede cambiarlo a su moneda local desde la Api
-- (GET/PUT /api/empresa) sin tocar código ni releases.
-- ============================================================

ALTER TABLE Empresa ADD
    CodigoMoneda    NVARCHAR(3) NOT NULL CONSTRAINT DF_Empresa_CodigoMoneda DEFAULT 'USD',   -- ISO 4217: USD, GTQ, HNL, NIO, CRC, MXN...
    SimboloMoneda   NVARCHAR(5) NOT NULL CONSTRAINT DF_Empresa_SimboloMoneda DEFAULT '$';
