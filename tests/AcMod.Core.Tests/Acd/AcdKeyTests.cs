using AcMod.Core.Acd;

namespace AcMod.Core.Tests.Acd;

public class AcdKeyTests
{
    // The "Known keys" table in docs/ACD.md: outputs printed by acd.py, used here as a black-box oracle.
    [Theory]
    [InlineData("testing", "254-7-113-249-206-21-55-104")]
    [InlineData("test", "192-45-0-55-66-241-55-117")]
    [InlineData("t", "116-0-0-131-66-101-171-171")]
    [InlineData("klmno", "33-150-122-205-66-141-64-112")]
    [InlineData("pqrstu", "175-71-117-68-22-131-59-118")]
    [InlineData("vwxyz", "88-191-111-161-66-119-53-123")]
    [InlineData("acm_tutorial_basic", "101-99-107-127-42-28-74-100")]
    [InlineData("zz1x", "157-46-0-96-66-235-49-121")]
    [InlineData("bmw_m3_e30", "108-96-216-121-166-192-73-49")]
    [InlineData("abarth500", "7-248-6-221-246-250-21-49")]
    [InlineData("ks_audi_sport_quattro", "230-190-101-8-154-33-64-112")]
    [InlineData("ks_porsche_911_gt3_r_2016", "145-191-144-93-26-0-15-55")]
    [InlineData("a1b2c3d4e5", "238-123-22-246-174-219-74-54")]
    [InlineData("shelby_cobra_427sc", "191-230-129-55-250-90-6-100")]
    public void Matches_the_known_keys(string name, string key)
    {
        Assert.Equal(key, AcdKey.FromName(name));
    }

    [Fact]
    public void Lowercases_the_name_first()
    {
        Assert.Equal(AcdKey.FromName("acm_tutorial_basic"), AcdKey.FromName("ACM_Tutorial_Basic"));
    }

    [Fact]
    public void Long_names_wrap_around_instead_of_throwing()
    {
        Assert.Matches(@"^(\d{1,3}-){7}\d{1,3}$", AcdKey.FromName(new string('z', 500)));
    }

    // acd.py's test paths, plus a real Windows car path.
    [Theory]
    [InlineData("a/testing", "testing")]
    [InlineData("klmno/data", "klmno")]
    [InlineData("pqrstu/data.acd", "pqrstu")]
    [InlineData("vwxyz/DAtAbaNAnA$", "vwxyz")]
    [InlineData(@"D:\ACModding\acm_tutorial_basic\data.acd", "acm_tutorial_basic")]
    public void Data_files_take_the_folder_name_and_others_their_own(string path, string source)
    {
        Assert.Equal(source, AcdKey.SourceName(path));
    }

    [Fact]
    public void ForPath_uses_the_folder_name()
    {
        Assert.Equal("101-99-107-127-42-28-74-100", AcdKey.ForPath(@"D:\ACModding\acm_tutorial_basic\data.acd"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("caf\u00e9")]
    public void Rejects_empty_and_non_ascii_names(string name)
    {
        Assert.Throws<ArgumentException>(() => AcdKey.FromName(name));
    }

    [Fact]
    public void Rejects_a_data_file_with_no_folder()
    {
        Assert.Throws<ArgumentException>(() => AcdKey.ForPath("data.acd"));
    }
}
