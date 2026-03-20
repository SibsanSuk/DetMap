using DetMap.Layers;

namespace DetMap.Tests.Layers;

public class DetTagLayerTests
{
    [Fact]
    public void AddTag_HasTag_ReturnsTrue()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(3, 3, "water");
        Assert.True(map.HasTag(3, 3, "water"));
    }

    [Fact]
    public void HasTag_MissingTag_ReturnsFalse()
    {
        var map = new DetTagLayer("services", 16, 16);
        Assert.False(map.HasTag(3, 3, "water"));
    }

    [Fact]
    public void AddTag_Duplicate_StoredOnce()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(1, 1, "temple");
        map.AddTag(1, 1, "temple");
        Assert.Equal(1, map.CountAt(1, 1));
    }

    [Fact]
    public void AddTag_MultipleTags_SameCell()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(2, 2, "water");
        map.AddTag(2, 2, "market");
        map.AddTag(2, 2, "temple");
        Assert.Equal(3, map.CountAt(2, 2));
        Assert.True(map.HasTag(2, 2, "water"));
        Assert.True(map.HasTag(2, 2, "market"));
        Assert.True(map.HasTag(2, 2, "temple"));
    }

    [Fact]
    public void RemoveTag_TagGone()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(5, 5, "water");
        map.RemoveTag(5, 5, "water");
        Assert.False(map.HasTag(5, 5, "water"));
        Assert.Equal(0, map.CountAt(5, 5));
    }

    [Fact]
    public void RemoveTag_OneOfMany_OthersRemain()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(4, 4, "water");
        map.AddTag(4, 4, "temple");
        map.RemoveTag(4, 4, "water");
        Assert.False(map.HasTag(4, 4, "water"));
        Assert.True(map.HasTag(4, 4, "temple"));
        Assert.Equal(1, map.CountAt(4, 4));
    }

    [Fact]
    public void RemoveTag_NonExistent_NoError()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.RemoveTag(0, 0, "nonexistent"); // should not throw
    }

    [Fact]
    public void HasAllTags_AllPresent_ReturnsTrue()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(6, 6, "water");
        map.AddTag(6, 6, "food");
        map.AddTag(6, 6, "temple");
        Assert.True(map.HasAllTags(6, 6, ["water", "food", "temple"]));
    }

    [Fact]
    public void HasAllTags_OneMissing_ReturnsFalse()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(7, 7, "water");
        map.AddTag(7, 7, "food");
        Assert.False(map.HasAllTags(7, 7, ["water", "food", "temple"]));
    }

    [Fact]
    public void HasAllTags_EmptyCell_ReturnsFalse()
    {
        var map = new DetTagLayer("services", 16, 16);
        Assert.False(map.HasAllTags(0, 0, ["water"]));
    }

    [Fact]
    public void GetTags_ReturnsAllTags()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(3, 3, "water");
        map.AddTag(3, 3, "market");
        var tags = map.GetTags(3, 3);
        Assert.Contains("water", tags);
        Assert.Contains("market", tags);
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void GetTags_EmptyCell_ReturnsEmpty()
    {
        var map = new DetTagLayer("services", 16, 16);
        Assert.Empty(map.GetTags(0, 0));
    }

    [Fact]
    public void CountAt_EmptyCell_IsZero()
    {
        var map = new DetTagLayer("services", 16, 16);
        Assert.Equal(0, map.CountAt(0, 0));
    }

    [Fact]
    public void AddTag_MarksDirty()
    {
        var map = new DetTagLayer("services", 16, 16);
        Assert.False(map.Dirty.IsDirty);
        map.AddTag(2, 3, "water");
        Assert.True(map.Dirty.IsDirty);
    }

    [Fact]
    public void ClearDirty_ResetsDirty()
    {
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(1, 1, "water");
        map.ClearDirty();
        Assert.False(map.Dirty.IsDirty);
    }

    [Fact]
    public void HousingEvolution_AllServicesPresent_Evolves()
    {
        // Scenario: a house cell evolves if it has water + food + temple coverage
        var map = new DetTagLayer("services", 16, 16);
        map.AddTag(5, 5, "water");
        map.AddTag(5, 5, "food");
        map.AddTag(5, 5, "temple");

        bool canEvolve = map.HasAllTags(5, 5, ["water", "food", "temple"]);
        Assert.True(canEvolve);
    }
}
