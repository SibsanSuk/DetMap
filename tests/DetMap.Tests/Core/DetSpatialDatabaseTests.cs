using DetMath;
using DetMap.Core;
using DetMap.Schema;

namespace DetMap.Tests.Core;

public class DetSpatialDatabaseTests
{
    [Fact]
    public void GetSchema_ReturnsDatabaseMetadataForInspectorUse()
    {
        var database = new DetSpatialDatabase(32, 16);
        database.Grid.CreateFix64Layer("height");
        database.Grid.CreateBitLayer("walkable");
        database.Grid.CreateCellIndex("units");

        var workers = database.CreateTable("workers");
        workers.CreateStringColumn("name");
        workers.CreateIntColumn("hp");

        database.SetGlobal("population", Fix64.FromInt(12));
        database.SetGlobal("treasury", Fix64.FromInt(500));
        database.CreatePathStore("workerPaths");

        DetDatabaseSchema schema = database.GetSchema();

        Assert.Equal(32, schema.Width);
        Assert.Equal(16, schema.Height);

        Assert.Equal(new[] { "height", "walkable", "units" }, schema.Layers.Select(x => x.Name).ToArray());
        Assert.Equal(new[] { DetLayerKind.ValueFix64, DetLayerKind.Bit, DetLayerKind.CellIndex }, schema.Layers.Select(x => x.Kind).ToArray());

        Assert.Single(schema.Tables);
        Assert.Equal("workers", schema.Tables[0].Name);
        Assert.Equal(new[] { "name", "hp" }, schema.Tables[0].Columns.Select(x => x.Name).ToArray());

        Assert.Equal(new[] { "population", "treasury" }, schema.GlobalKeys.ToArray());

        Assert.Single(schema.Stores);
        Assert.Equal("workerPaths", schema.Stores[0].Name);
        Assert.Equal(DetStoreKind.Path, schema.Stores[0].Kind);
    }
}
