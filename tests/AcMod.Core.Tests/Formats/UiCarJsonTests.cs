using System.Globalization;
using AcMod.Core.Formats;

namespace AcMod.Core.Tests.Formats;

public class UiCarJsonTests
{
    private static UiCarJson Load(string name) => UiCarJson.Parse(File.ReadAllBytes(Fixtures.Synthetic("json", name)));

    [Fact]
    public void Reads_kunos_style_strings_with_raw_line_breaks_and_escaped_quotes()
    {
        var json = Load("kunos_style.json");

        Assert.Null(json.Error);
        Assert.Equal("Test Car", json.GetString("/name"));
        Assert.Equal("First line.</br>\r\n- raw line break inside the string\r\n- a \"quoted\" word", json.GetString("/description"));
        Assert.Equal("198 bhp", json.GetString("/specs/bhp"));
        Assert.Equal("130", json.GetString("/torqueCurve/1/1"));
        Assert.Equal("rwd", json.GetString("/tags/1"));
    }

    [Fact]
    public void Reads_numbers_bom_and_non_ascii()
    {
        var json = Load("mod_numbers.json");

        Assert.Null(json.Error);
        Assert.True(json.Document.HasBom);
        Assert.Equal("Café Racer", json.GetString("/name"));
        Assert.Equal(250, json.GetNumber("/specs/bhp"));
        Assert.Equal(35.5, json.GetNumber("/powerCurve/1/1"));
        Assert.Null(json.GetNumber("/specs/pwratio")); // null is neither a string nor a number
        Assert.Null(json.GetString("/missing"));
    }

    [Fact]
    public void Reports_invalid_json_but_still_round_trips_and_refuses_edits()
    {
        var bytes = File.ReadAllBytes(Fixtures.Synthetic("json", "broken.json"));

        var json = UiCarJson.Parse(bytes);

        Assert.NotNull(json.Error);
        Assert.Equal(bytes, json.ToBytes());
        Assert.Throws<InvalidOperationException>(() => json.WithString("/name", "x"));
    }

    // QS-2: an edit replaces only that value's characters. FixtureRunnerTests repeats this for every fixture value.
    [Fact]
    public void Editing_a_string_changes_only_that_value()
    {
        var json = Load("kunos_style.json");
        var before = json.Document.Text;

        var edited = json.WithString("/specs/bhp", "210 bhp");

        Assert.Equal(before.Replace("\"198 bhp\"", "\"210 bhp\""), edited.Document.Text);
        Assert.Equal("210 bhp", edited.GetString("/specs/bhp"));
    }

    [Fact]
    public void Escapes_quotes_backslashes_and_line_breaks_in_new_strings()
    {
        var value = "A \"fast\" car\\\nsecond line\ttab";

        var edited = Load("kunos_style.json").WithString("/name", value);

        Assert.Contains("\"name\": \"A \\\"fast\\\" car\\\\\\nsecond line\\ttab\",", edited.Document.Text);
        Assert.Equal(value, UiCarJson.Parse(edited.ToBytes()).GetString("/name"));
    }

    [Fact]
    public void Editing_a_number_keeps_the_bom_and_uses_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("id-ID");
        try
        {
            var edited = Load("mod_numbers.json").WithNumber("/powerCurve/1/1", 36.25);

            Assert.Contains("[1000, 36.25]", edited.Document.Text);
            Assert.True(edited.Document.HasBom);
            Assert.Equal(36.25, edited.GetNumber("/powerCurve/1/1"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Refuses_edits_that_would_change_a_value_type_or_target_nothing()
    {
        var json = Load("mod_numbers.json");

        Assert.Throws<ArgumentException>(() => json.WithString("/specs/bhp", "250 bhp")); // a number, not a string
        Assert.Throws<ArgumentException>(() => json.WithNumber("/name", 1));               // a string, not a number
        Assert.Throws<ArgumentException>(() => json.WithString("/missing", "x"));
        Assert.Throws<ArgumentException>(() => json.WithNumber("/specs/bhp", double.NaN));
    }
}
