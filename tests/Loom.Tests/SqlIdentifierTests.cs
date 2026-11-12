using Loom.Spool;

namespace Loom.Tests;

public class SqlIdentifierTests
{
    [Fact]
    public void Quote_SafeIdentifier_BracketsIt()
    {
        Assert.Equal("[RelationTuples]", SqlIdentifier.Quote("RelationTuples"));
    }

    [Fact]
    public void Quote_SchemaAndTable_ComposesQualifiedName()
    {
        Assert.Equal("[tenant_1].[RelationTuples]", SqlIdentifier.Quote("tenant_1", "RelationTuples"));
    }

    [Theory]
    [InlineData("Robert'); DROP TABLE Students;--")]
    [InlineData("1table")]
    [InlineData("has space")]
    public void Quote_UnsafeIdentifier_Throws(string identifier)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.Quote(identifier));
    }
}
