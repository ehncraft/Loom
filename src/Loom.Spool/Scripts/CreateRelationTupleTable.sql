-- Provisions the table SqlRelationTupleStore reads/writes, using the default column names from
-- SqlRelationTupleStoreOptions. Rename freely -- every name here is just the default, not a
-- requirement; point SqlRelationTupleStoreOptions at whatever this ends up being called.
--
-- Multi-tenancy is structural: run this once per tenant schema (e.g. tenant_123.RelationTuples),
-- not once against a shared table with a tenant-id column -- see SqlRelationTupleStoreOptions'
-- own doc comment.
--
-- SubjectRelation and CaveatName are NOT NULL DEFAULT '' (empty string means "unset") rather
-- than nullable, so both can participate in the primary key -- SQL Server treats NULL as
-- distinct-from-itself for uniqueness purposes, which would let the same tuple with no caveat
-- be inserted twice. CaveatContext stays nullable (NVARCHAR(MAX) can't be part of a primary
-- key); it's JSON, present only when CaveatName is set.
CREATE TABLE RelationTuples
(
    ObjectNamespace  NVARCHAR(200)  NOT NULL,
    ObjectId         NVARCHAR(200)  NOT NULL,
    Relation         NVARCHAR(200)  NOT NULL,
    SubjectNamespace NVARCHAR(200)  NOT NULL,
    SubjectId        NVARCHAR(200)  NOT NULL,
    SubjectRelation  NVARCHAR(200)  NOT NULL DEFAULT '',
    CaveatName       NVARCHAR(200)  NOT NULL DEFAULT '',
    CaveatContext    NVARCHAR(MAX)  NULL,
    CONSTRAINT PK_RelationTuples PRIMARY KEY (
        ObjectNamespace, ObjectId, Relation, SubjectNamespace, SubjectId, SubjectRelation, CaveatName
    )
);

-- ResourcesOfNamespaceAsync/ReadAsync are already served by the primary key's own clustered
-- index (ObjectNamespace/ObjectId are its leading columns). SubjectsOfNamespaceAsync isn't --
-- SubjectNamespace is the fourth PK column, not a usable prefix -- hence this one.
CREATE INDEX IX_RelationTuples_SubjectNamespace ON RelationTuples (SubjectNamespace, SubjectId, SubjectRelation);
