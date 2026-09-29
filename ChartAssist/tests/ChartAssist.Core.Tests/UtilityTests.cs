namespace ChartAssist.Core.Tests;

public sealed class UtilityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("EDFM Mannheim City 1", "EDFM Mannheim City 1")] // unverändert wie in ChartButlerCS
    [InlineData("EDFM AD 2-67", "EDFM AD 2-67")]
    [InlineData("Mannheim City/2B", "Mannheim City-2B")]
    [InlineData("A\\B<C>D", "A-B-C-D")]
    [InlineData("Karte \"Nord\" o'Brien", "Karte Nord oBrien")]
    [InlineData("Zeit: 12:00", "Zeit- 12-00")] // unter Windows verboten
    [InlineData("Wer*Wo?Was|", "Wer-Wo-Was-")]
    [InlineData("  Name . . ", "Name")]
    [InlineData("Tab\tName", "TabName")]
    [InlineData("CON", "_CON")]
    [InlineData("com1.png", "_com1.png")]
    [InlineData("CONTROL", "CONTROL")]
    public void GetFilenameFor_BildetWindowsTauglicheNamen(string name, string expected)
    {
        Assert.Equal(expected, Utility.GetFilenameFor(name));
    }

    [Theory]
    [InlineData("EDFM - Mannheim City", true)]
    [InlineData("ED01 - Name", true)]
    [InlineData("Import", false)]
    [InlineData("edfm - klein", false)]
    [InlineData("EDFM - ", false)]
    [InlineData("TEMP.abc", false)]
    public void IsAirfieldDirectoryName(string name, bool expected)
    {
        Assert.Equal(expected, Utility.IsAirfieldDirectoryName(name));
    }

    [Fact]
    public void FileEquals_VergleichtInhalt()
    {
        string a = Path.Combine(_root, "a");
        string b = Path.Combine(_root, "b");
        File.WriteAllBytes(a, [1, 2, 3]);
        File.WriteAllBytes(b, [1, 2, 3]);

        Assert.True(Utility.FileEquals(a, b));
        Assert.True(Utility.FileEquals(a, [1, 2, 3]));
        Assert.False(Utility.FileEquals(a, [1, 2, 4]));
        Assert.False(Utility.FileEquals(a, Path.Combine(_root, "fehlt")));
    }

    [Fact]
    public void WriteFileAtomic_ErsetztDateiUndHinterlaesstKeineTempDatei()
    {
        string path = Path.Combine(_root, "datei");
        File.WriteAllBytes(path, [9]);

        Utility.WriteFileAtomic(path, [1, 2]);

        Assert.Equal([1, 2], File.ReadAllBytes(path));
        Assert.Single(Directory.GetFileSystemEntries(_root));
    }
}
