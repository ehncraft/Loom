-- Provisions the table SqlSchemaStore reads/writes, using the default column names from
-- SqlSchemaStoreOptions. Rename freely -- every name here is just the default.
--
-- One schema document per tenant (run this once per tenant schema, same multi-tenancy
-- convention as CreateRelationTupleTable.sql): SqlSchemaStore.SaveAsync replaces the table's
-- entire contents rather than updating a single row in place, so there's no primary key here
-- to enforce -- SchemaText holds the whole parsed-as-one-document schema DSL text (every
-- `definition` together), not one row per definition.
CREATE TABLE Schemas
(
    SchemaText NVARCHAR(MAX) NOT NULL,
    UpdatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
