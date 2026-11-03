using System.Text;
using AcMod.Core.Acd;
using AcMod.Core.Tuning;

namespace AcMod.Core.Tests.Tuning;

public sealed class TuningFormsTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Each_field_gets_its_own_comment_or_borrows_the_same_keys_comment()
    {
        var fields = TuningForms.Fields(Bytes(
            "[FRONT]\r\nSPRING_RATE=80000\t\t; Wheel rate stiffness in Nm\r\nTRACK=1.5 // track width\r\n" +
            "[REAR]\r\nSPRING_RATE=90000\r\nTRACK=1.6\r\nNOTE=x\r\n"));

        Assert.Equal(
            [
                new FormField("[FRONT] SPRING_RATE", "FRONT", "SPRING_RATE", "80000", 1, "Wheel rate stiffness in Nm", null),
                new FormField("[FRONT] TRACK", "FRONT", "TRACK", "1.5", 2, "track width", null),
                new FormField("[REAR] SPRING_RATE", "REAR", "SPRING_RATE", "90000", 4, "Wheel rate stiffness in Nm", "[FRONT]"),
                new FormField("[REAR] TRACK", "REAR", "TRACK", "1.6", 5, "track width", "[FRONT]"),
                new FormField("[REAR] NOTE", "REAR", "NOTE", "x", 6, null, null), // no comment anywhere
            ],
            fields);
    }

    [Fact]
    public void A_repeated_key_is_numbered_like_the_diff()
    {
        var fields = TuningForms.Fields(Bytes("[INFO]\r\nTAG=a\r\nTAG=b\r\n"));

        Assert.Equal(["[INFO] TAG", "[INFO] TAG #2"], fields.Select(f => f.Id));
    }

    [Theory]
    [InlineData("0.120", "0,13", "0.13")] // Indonesian decimal comma on a single number
    [InlineData("6500", " 5500 ", "5500")]
    [InlineData("0,-0.65,0.05", "0,-0.6,0.05", "0,-0.6,0.05")] // a vector keeps its commas
    [InlineData("0.5", "1,2,3", "1,2,3")] // more than one comma isn't a decimal
    [InlineData("power.lut", "a,b", "a,b")] // not a number
    public void Normalize_turns_a_decimal_comma_into_a_point_only_for_single_numbers(string current, string typed, string stored)
    {
        Assert.Equal(stored, TuningForms.Normalize(current, typed));
    }

    [Fact]
    public void The_tutorial_cars_limiter_gets_its_own_comment_as_the_hint()
    {
        var car = Path.Combine(Fixtures.LocalRoot, "acm_tutorial_basic");
        if (!Directory.Exists(car)) return; // gitignored real car, missing on a fresh clone

        var engine = CarData.Load(car).Files.Single(f => f.Name == "engine.ini");
        var limiter = TuningForms.Fields(engine.Data).Single(f => f.Id == "[ENGINE_DATA] LIMITER");

        Assert.Equal(("6500", "engine rev limiter. 0 no limiter", null), (limiter.Value, limiter.Hint, limiter.HintFrom));
    }
}
