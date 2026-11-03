using AcMod.Core.Acd;
using AcMod.Core.Validation;

namespace AcMod.Core.Tests.Validation;

/// <summary>
/// The M5 exit gate: each rule has a passing fixture (<see cref="GoodCar"/> gives no finding at all) and a failing one (one test per rule
/// breaks exactly one thing). Fixtures are built in temp folders (owner decision, 2026-11-03).
/// </summary>
public sealed class ValidatorTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("acm_validator_").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static readonly string[] DataFiles =
    [
        "aero.ini", "cameras.ini", "car.ini", "driver3d.ini", "engine.ini", "lods.ini", "suspensions.ini", "tyres.ini", "power.lut",
        "brakes.ini", "colliders.ini", "drivetrain.ini", "setup.ini",
    ];

    /// <summary>A car that passes every rule.</summary>
    private string GoodCar(string carId = "acm_good")
    {
        var car = Path.Combine(_temp, carId);
        Put(car, "collider.kn5", "kn5");
        Put(car, "car_main.kn5", "kn5");
        Put(car, @"skins\default\preview.jpg", "jpg");
        foreach (var file in DataFiles) Put(car, Path.Combine("data", file), file.EndsWith(".lut") ? "0|100\r\n8000|200\r\n" : "[HEADER]\r\nVERSION=1\r\n");
        Put(car, @"data\lods.ini", "[LOD_0]\r\nFILE=Car_Main.kn5\r\nOUT=600\r\n"); // case differs from the file, as in formula_k
        Put(car, @"data\engine.ini", "[HEADER]\r\nPOWER_CURVE=POWER.lut\r\nCOAST_CURVE=FROM_COAST_REF\r\n[COAST_CURVE]\r\nFILENAME=coast.lut\r\n"); // AC ignores this one
        Put(car, $@"sfx\{carId.ToUpperInvariant()}.bank", "bank"); // case ignored
        Put(car, @"sfx\GUIDs.txt", $"{{00000000-0000-0000-0000-000000000000}} bank:/{carId}\r\n{{00000000-0000-0000-0000-000000000001}} event:/cars/{carId}/engine_ext\r\n");
        Put(car, @"ui\ui_car.json", "{\"name\": \"Good car\",\r\n\"description\": \"raw\r\nline break\"}"); // only lenient JSON allows that
        File.WriteAllBytes(Path.Combine(car, "ui", "badge.png"), Png(128, 128));
        return car;
    }

    /// <summary>The PNG signature and an IHDR header with this size; the validator reads nothing more.</summary>
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    private static void Put(string car, string path, string text)
    {
        var full = Path.Combine(car, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private static string[] Rules(string car) => [.. Validator.Run(car).Select(f => $"{f.Rule} {f.Severity} {f.File}")];

    [Fact]
    public void A_complete_car_has_no_findings()
    {
        Assert.Empty(Validator.Run(GoodCar()));
    }

    [Theory]
    [InlineData("acm_Good")]
    [InlineData("acm good")]
    [InlineData("acm_güt")]
    public void V01_a_car_ID_that_isnt_lowercase_ASCII_is_a_warning(string carId)
    {
        Assert.Equal(["V-01 Warning "], Rules(GoodCar(carId)));
    }

    [Fact]
    public void V04_a_missing_collider_is_an_error()
    {
        var car = GoodCar();
        File.Delete(Path.Combine(car, "collider.kn5"));

        Assert.Equal(["V-04 Error collider.kn5"], Rules(car));
    }

    [Fact]
    public void V05_a_missing_mandatory_data_file_is_an_error()
    {
        var car = GoodCar();
        File.Delete(Path.Combine(car, "data", "power.lut"));

        Assert.Equal(["V-05 Error power.lut"], Rules(car));
    }

    [Fact]
    public void V06_a_missing_behaviour_file_is_a_warning()
    {
        var car = GoodCar();
        File.Delete(Path.Combine(car, "data", "setup.ini"));

        Assert.Equal(["V-06 Warning setup.ini"], Rules(car));
    }

    [Fact]
    public void V05_and_V06_check_data_acd_when_it_exists()
    {
        var car = GoodCar();
        File.Delete(Path.Combine(car, "data", "brakes.ini"));
        File.WriteAllBytes(Path.Combine(car, "data.acd"), CarData.PackFolder(car).Bytes!);
        Directory.Delete(Path.Combine(car, "data"), recursive: true);

        Assert.Equal(["V-06 Warning brakes.ini"], Rules(car)); // read from data.acd, the effective data
    }

    [Fact]
    public void V02_a_model_lods_ini_names_but_the_folder_lacks_is_an_error()
    {
        var car = GoodCar();
        File.Delete(Path.Combine(car, "car_main.kn5"));

        Assert.Equal(["V-02 Error lods.ini"], Rules(car));
    }

    [Fact]
    public void V07_a_table_a_data_file_names_but_the_data_lacks_is_an_error()
    {
        var car = GoodCar();
        Put(car, @"data\tyres.ini", "[FRONT]\r\nWEAR_CURVE=front.lut\r\n[__CM_FRONT_ORIGINAL]\r\nWEAR_CURVE=old.lut\r\n"); // the backup section is skipped
        Put(car, @"data\tyres - copy.ini", "[FRONT]\r\nWEAR_CURVE=gone.lut\r\n"); // AC doesn't read stray copies

        var finding = Assert.Single(Validator.Run(car));

        Assert.Equal(("V-07", Severity.Error, "tyres.ini"), (finding.Rule, finding.Severity, finding.File));
        Assert.Contains("[FRONT] WEAR_CURVE names front.lut", finding.Message);
        Put(car, @"data\front.lut", "0|1\r\n");
        Assert.Empty(Validator.Run(car));
    }

    [Fact]
    public void V08_digital_instruments_ini_is_info()
    {
        var car = GoodCar();
        Put(car, @"data\digital_instruments.ini", "[LED_0]\r\nOBJECT_NAME=LED_0\r\n");

        Assert.Equal(["V-08 Info digital_instruments.ini"], Rules(car));
    }

    [Fact]
    public void V09_a_missing_bank_is_an_error()
    {
        var car = GoodCar();
        File.Delete(Path.Combine(car, "sfx", "ACM_GOOD.bank"));

        Assert.Equal([@"V-09 Error sfx\acm_good.bank"], Rules(car));
    }

    [Fact]
    public void V09_GUIDs_may_come_from_ACs_shared_file_like_Kunos_cars()
    {
        var car = GoodCar();
        Put(car, @"sfx\GUIDs.txt", "{00000000-0000-0000-0000-000000000000} bank:/someone_else\r\n");
        var ac = Path.Combine(_temp, "ac");

        Assert.Equal([@"V-09 Error sfx\GUIDs.txt"], [.. Validator.Run(car, ac).Select(f => $"{f.Rule} {f.Severity} {f.File}")]);
        Put(ac, @"content\sfx\GUIDs.txt", "{00000000-0000-0000-0000-000000000000} bank:/common\r\n{00000000-0000-0000-0000-000000000001} event:/cars/ACM_GOOD/engine_int\r\n");
        Assert.Empty(Validator.Run(car, ac));
        Assert.Contains("AC folder isn't set", Validator.Run(car).Single().Message);
    }

    [Theory]
    [InlineData(@"ui\ui_car.json", "{\"name\": ", "V-10 Warning ui\\ui_car.json")]
    [InlineData(@"ui\badge.png", "not a png", "V-10 Warning ui\\badge.png")]
    public void V10_broken_ui_files_are_warnings(string path, string text, string rule)
    {
        var car = GoodCar();
        Put(car, path, text);

        Assert.Equal([rule], Rules(car));
    }

    [Fact]
    public void V10_a_badge_that_isnt_128_square_is_info()
    {
        var car = GoodCar();
        File.WriteAllBytes(Path.Combine(car, "ui", "badge.png"), Png(256, 128));

        var finding = Assert.Single(Validator.Run(car));
        Assert.Equal(("V-10", Severity.Info), (finding.Rule, finding.Severity));
        Assert.Contains("256×128", finding.Message);
    }

    [Fact]
    public void V11_a_car_without_a_skin_folder_is_a_warning()
    {
        var car = GoodCar();
        Directory.Delete(Path.Combine(car, "skins", "default"), recursive: true);

        Assert.Equal(["V-11 Warning skins"], Rules(car));
    }

    [Fact]
    public void V12_data_folder_next_to_data_acd_is_a_warning()
    {
        var car = GoodCar();
        File.WriteAllBytes(Path.Combine(car, "data.acd"), CarData.PackFolder(car).Bytes!);

        Assert.Equal(["V-12 Warning data"], Rules(car));
    }

    [Fact]
    public void V13_unreadable_data_acd_is_an_error_and_skips_the_data_rules()
    {
        var car = GoodCar();
        File.WriteAllText(Path.Combine(car, "data.acd"), "not an acd file");
        Directory.Delete(Path.Combine(car, "data"), recursive: true);

        var finding = Assert.Single(Validator.Run(car));
        Assert.Equal(("V-13", Severity.Error, "data.acd"), (finding.Rule, finding.Severity, finding.File));
        Assert.Contains("never bypasses protection", finding.Fix);
    }

    [Fact]
    public void V15_development_files_are_info()
    {
        var car = GoodCar();
        Put(car, "car.blend", "x");
        Put(car, @"texture\NULL.dds", "x");
        Put(car, @"skins\default\livery.PSD", "x");

        Assert.Equal(["V-15 Info car.blend", @"V-15 Info skins\default\livery.PSD", @"V-15 Info texture\NULL.dds"], Rules(car));
    }

    [Fact]
    public void Every_finding_has_a_message_a_fix_and_an_evidence_label()
    {
        var car = GoodCar("acm_Bad");
        File.Delete(Path.Combine(car, "collider.kn5"));
        File.Delete(Path.Combine(car, "data", "aero.ini"));

        Assert.All(Validator.Run(car), f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Message));
            Assert.False(string.IsNullOrWhiteSpace(f.Fix));
            Assert.True(Enum.IsDefined(f.Evidence));
        });
    }
}
