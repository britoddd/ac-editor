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

    // QS-1: every ui_car.json fixture, including real cars in fixtures/local, is written back byte-for-byte.
    [Fact]
    public void Every_fixture_ui_car_json_round_trips_byte_for_byte()
    {
        var files = UiJsonFixtures().ToList();

        var failures = files.Where(f =>
        {
            var bytes = File.ReadAllBytes(f);
            return !bytes.AsSpan().SequenceEqual(UiCarJson.Parse(bytes).ToBytes());
        }).ToList();

        Assert.NotEmpty(files);
        Assert.Empty(failures);
    }

    // QS-2: an edit replaces only that value's characters.
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

    // QS-2 on real data: in every fixture ui_car.json, changing any one string leaves every other string the same.
    [Fact]
    public void Editing_any_real_string_leaves_the_others_unchanged()
    {
        var checkedValues = 0;
        foreach (var path in UiJsonFixtures())
        {
            var json = UiCarJson.Parse(File.ReadAllBytes(path));
            if (json.Error != null)
            {
                continue;
            }

            foreach (var pointer in StringPointers(json))
            {
                var edited = UiCarJson.Parse(json.WithString(pointer, "x").ToBytes());

                Assert.Null(edited.Error);
                Assert.All(StringPointers(json), other =>
                    Assert.Equal(other == pointer ? "x" : json.GetString(other), edited.GetString(other)));
                checkedValues++;
            }
        }

        Assert.True(checkedValues > 0);
    }

    private static IEnumerable<string> UiJsonFixtures()
    {
        var synthetic = Directory.EnumerateFiles(Fixtures.Synthetic("json"), "*.json");
        var local = Directory.Exists(Fixtures.LocalRoot)
            ? Directory.EnumerateFiles(Fixtures.LocalRoot, "ui_car.json", SearchOption.AllDirectories)
            : [];
        return synthetic.Concat(local);
    }

    // Probes the common ui_car.json paths. The reader has no public list of pointers, which version 1 doesn't need yet.
    private static IEnumerable<string> StringPointers(UiCarJson json)
    {
        string[] fixedPaths = ["/name", "/brand", "/description", "/class", "/country", "/author", "/version", "/url", "/year"];
        var specs = new[] { "bhp", "torque", "weight", "topspeed", "acceleration", "pwratio", "range" }.Select(s => "/specs/" + s);
        var curves = from curve in new[] { "/torqueCurve", "/powerCurve" }
                     from i in Enumerable.Range(0, 40)
                     from j in new[] { 0, 1 }
                     select $"{curve}/{i}/{j}";
        var tags = Enumerable.Range(0, 20).Select(i => $"/tags/{i}");
        return fixedPaths.Concat(specs).Concat(curves).Concat(tags).Where(p => json.GetString(p) != null);
    }
}
