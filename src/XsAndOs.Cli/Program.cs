using XsAndOs.Core;
using XsAndOs.Playbook;

return args switch
{
    ["export-samples", var dir] => ExportSamples(dir),
    _ => Usage(),
};

static int ExportSamples(string dir)
{
    Directory.CreateDirectory(dir);
    foreach (var play in SamplePlays.All)
    {
        var path = Path.Combine(dir, play.Name + ".json");
        PlaybookSerializer.SaveToFile(play, path);
        Console.WriteLine($"wrote {path}");
    }

    return 0;
}

static int Usage()
{
    Console.WriteLine("usage: xso export-samples <dir>");
    return 1;
}
