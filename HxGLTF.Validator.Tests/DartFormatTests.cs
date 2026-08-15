using System.Text.Json;
using HxGLTF.Validator.Internal;

namespace HxGLTF.Validator.Tests;

public class DartFormatTests
{
    [Fact]
    public void Doubles_Follow_Dart_ToString()
    {
        Assert.Equal("1.0", DartFormat.V(1.0));
        Assert.Equal("-1.0", DartFormat.V(-1.0));
        Assert.Equal("0.5", DartFormat.V(0.5));
        Assert.Equal("0.0", DartFormat.V(0.0));
        Assert.Equal("-0.0", DartFormat.V(-0.0));
        Assert.Equal("1e-7", DartFormat.V(1e-7));
        Assert.Equal("0.000001", DartFormat.V(1e-6));
        Assert.Equal("1e+21", DartFormat.V(1e21));
        Assert.Equal("100000000000000000000.0", DartFormat.V(1e20));
        Assert.Equal("123456789012.0", DartFormat.V(123456789012.0));
        Assert.Equal("0.30000000000000004", DartFormat.V(0.1 + 0.2));
        Assert.Equal("1.7320508075688772", DartFormat.V(Math.Sqrt(3.0)));
        Assert.Equal("1.5e-7", DartFormat.V(1.5e-7));
        Assert.Equal("1.2345e+25", DartFormat.V(1.2345e25));
        Assert.Equal("NaN", DartFormat.V(double.NaN));
        Assert.Equal("Infinity", DartFormat.V(double.PositiveInfinity));
        Assert.Equal("-Infinity", DartFormat.V(double.NegativeInfinity));
        Assert.Equal("2.0", DartFormat.V(2.0f));
    }

    [Fact]
    public void Ints_And_Misc_Follow_Dart_ToString()
    {
        Assert.Equal("1", DartFormat.V(1));
        Assert.Equal("-7", DartFormat.V(-7L));
        Assert.Equal("4294967295", DartFormat.V(4294967295L));
        Assert.Equal("null", DartFormat.V(null));
        Assert.Equal("true", DartFormat.V(true));
        Assert.Equal("false", DartFormat.V(false));
        Assert.Equal("abc", DartFormat.V("abc"));
    }

    [Fact]
    public void Lists_And_Iterables()
    {
        Assert.Equal("[1, 2]", DartFormat.V(new[] { 1, 2 }));
        Assert.Equal("[1.0, 2.5]", DartFormat.V(new List<double> { 1.0, 2.5 }));
        Assert.Equal("[]", DartFormat.V(Array.Empty<int>()));
        Assert.Equal("(a, b)", DartFormat.V(DartFormat.Iter(new[] { "a", "b" })));
        Assert.Equal("()", DartFormat.V(DartFormat.Iter(Array.Empty<string>())));
        Assert.Equal("[[1, 2], [3]]", DartFormat.V(new object[] { new[] { 1, 2 }, new[] { 3 } }));
    }

    [Fact]
    public void Quoting_Helpers()
    {
        Assert.Equal("'x'", DartFormat.Q("x"));
        Assert.Equal("'1'", DartFormat.Q(1));
        Assert.Equal("'null'", DartFormat.Q(null));
        Assert.Equal("'x'", DartFormat.Mbq("x"));
        Assert.Equal("1", DartFormat.Mbq(1));
        Assert.Equal("1.0", DartFormat.Mbq(1.0));
        Assert.Equal("null", DartFormat.Mbq(null));
        Assert.Equal("('a', 2)", DartFormat.MbqIter(new object?[] { "a", 2 }));
        Assert.Equal("('SCALAR', 'VEC2')", DartFormat.MbqIter(new[] { "SCALAR", "VEC2" }));
        Assert.Equal("(1, 2)", DartFormat.MbqIter(DartFormat.Iter(new[] { 1, 2 })));
        Assert.Equal("('a', '1')", IssueFormat.QIter(new object?[] { "a", 1 }));
    }

    [Fact]
    public void Json_Values_Print_Like_Dart()
    {
        using var doc = JsonDocument.Parse("{\"a\": 1, \"b\": [1.5, \"x\", null, true], \"c\": {}}");
        Assert.Equal("{a: 1, b: [1.5, x, null, true], c: {}}", DartFormat.V(doc.RootElement));
        using var arr = JsonDocument.Parse("[]");
        Assert.Equal("[]", DartFormat.V(arr.RootElement));
    }

    [Fact]
    public void Hex8()
    {
        Assert.Equal("0x004b4e55", DartFormat.Hex8(0x004b4e55));
        Assert.Equal("0x4e4b4e55", DartFormat.Hex8(0x4e4b4e55));
    }
}
