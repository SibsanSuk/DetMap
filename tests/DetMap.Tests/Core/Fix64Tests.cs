using DetMath;

namespace DetMap.Tests.Core;

/// <summary>Smoke tests for DetMath.Fix64 API used within DetMap.</summary>
public class Fix64Tests
{
    [Fact] public void Zero_IsZero() => Assert.Equal(Fix64.Zero, Fix64.FromInt(0));
    [Fact] public void One_IsOne() => Assert.Equal(Fix64.One, Fix64.FromInt(1));
    [Fact] public void FromInt_RoundTrips() => Assert.Equal(42, Fix64.FromInt(42).ToIntTruncate());

    [Fact]
    public void FromRatio_HalfIsPointFive()
    {
        Assert.Equal(Fix64.Half, Fix64.FromRatio(1, 2));
    }

    [Fact]
    public void Add_TwoValues_IsCorrect()
    {
        Assert.Equal(Fix64.FromInt(7), Fix64.FromInt(3) + Fix64.FromInt(4));
    }

    [Fact]
    public void Subtract_TwoValues_IsCorrect()
    {
        Assert.Equal(Fix64.FromInt(1), Fix64.FromInt(4) - Fix64.FromInt(3));
    }

    [Fact]
    public void Multiply_TwoValues_IsCorrect()
    {
        Assert.Equal(Fix64.FromInt(12), Fix64.FromInt(3) * Fix64.FromInt(4));
    }

    [Fact]
    public void Divide_TwoValues_IsCorrect()
    {
        Assert.Equal(Fix64.FromInt(5), Fix64.FromInt(10) / Fix64.FromInt(2));
    }

    [Fact]
    public void Comparison_LessThan_Works()
    {
        Assert.True(Fix64.FromInt(1) < Fix64.FromInt(2));
        Assert.False(Fix64.FromInt(2) < Fix64.FromInt(1));
    }

    [Fact]
    public void DetMathf_Min_Max_Abs_Work()
    {
        Assert.Equal(Fix64.FromInt(3), DetMathf.Min(Fix64.FromInt(3), Fix64.FromInt(7)));
        Assert.Equal(Fix64.FromInt(7), DetMathf.Max(Fix64.FromInt(3), Fix64.FromInt(7)));
        Assert.Equal(Fix64.FromInt(5), DetMathf.Abs(Fix64.FromInt(-5)));
    }

    [Fact]
    public void Determinism_SameInput_SameOutput()
    {
        var a = Fix64.FromInt(7);
        var b = Fix64.FromInt(3);
        var r1 = a * b - Fix64.FromInt(1);
        var r2 = a * b - Fix64.FromInt(1);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void ToIntFloor_NegativeFractional_FloorsDown()
    {
        // -1.50 → floor = -2
        var neg = Fix64.FromInt(0) - Fix64.FromRatio(3, 2);
        Assert.Equal(-2, neg.ToIntFloor());
        Assert.Equal(-1, neg.ToIntTruncate());
    }
}
