using See.Net.Core;

namespace See.Net.Tests;

public sealed class RangeRequestTests
{
    [Fact]
    public void Parse_Null_Or_Empty_Header_Returns_Null()
    {
        Assert.Null(RangeSpec.Parse(null, 1000));
        Assert.Null(RangeSpec.Parse("", 1000));
    }

    [Fact]
    public void Parse_Open_Ended_Range()
    {
        var spec = RangeSpec.Parse("bytes=0-", 1000);
        Assert.NotNull(spec);
        Assert.Equal(0, spec.Value.Start);
        Assert.Equal(999, spec.Value.End);
    }

    [Fact]
    public void Parse_Bounded_Range()
    {
        var spec = RangeSpec.Parse("bytes=100-199", 1000);
        Assert.NotNull(spec);
        Assert.Equal(100, spec.Value.Start);
        Assert.Equal(199, spec.Value.End);
    }

    [Fact]
    public void Parse_Clamps_End_To_Length()
    {
        var spec = RangeSpec.Parse("bytes=900-5000", 1000);
        Assert.NotNull(spec);
        Assert.Equal(900, spec.Value.Start);
        Assert.Equal(999, spec.Value.End);
    }

    [Fact]
    public void Parse_Suffix_Range()
    {
        var spec = RangeSpec.Parse("bytes=-500", 1000);
        Assert.NotNull(spec);
        Assert.Equal(500, spec.Value.Start);
        Assert.Equal(999, spec.Value.End);
    }

    [Fact]
    public void Parse_Suffix_Larger_Than_File()
    {
        var spec = RangeSpec.Parse("bytes=-5000", 1000);
        Assert.NotNull(spec);
        Assert.Equal(0, spec.Value.Start);
        Assert.Equal(999, spec.Value.End);
    }

    [Fact]
    public void Parse_Unsatisfiable_Start_Returns_Null()
    {
        Assert.Null(RangeSpec.Parse("bytes=1000-", 1000));
        Assert.Null(RangeSpec.Parse("bytes=2000-3000", 1000));
    }

    [Fact]
    public void Parse_Multiple_Ranges_Returns_Null()
    {
        Assert.Null(RangeSpec.Parse("bytes=0-99,200-299", 1000));
    }

    [Fact]
    public void Parse_Malformed_Returns_Null()
    {
        Assert.Null(RangeSpec.Parse("bytes=abc-", 1000));
        Assert.Null(RangeSpec.Parse("bytes=100-99", 1000));
        Assert.Null(RangeSpec.Parse("items=0-99", 1000));
        Assert.Null(RangeSpec.Parse("bytes=", 1000));
    }

    [Fact]
    public void Parse_Zero_Length_Returns_Null()
    {
        Assert.Null(RangeSpec.Parse("bytes=0-", 0));
    }
}
