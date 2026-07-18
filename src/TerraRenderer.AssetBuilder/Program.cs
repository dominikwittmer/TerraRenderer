using System.Text.Json;
using TerraRenderer.AssetBuilder;

try
{
    var configPath = ParseConfigPath(args);
    configPath = Path.GetFullPath(configPath);
    if (!File.Exists(configPath))
        throw new FileNotFoundException("AssetBuilder configuration was not found.", configPath);

    var json = File.ReadAllText(configPath);
    var config = JsonSerializer.Deserialize<AssetBuilderConfiguration>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    }) ?? throw new InvalidOperationException("AssetBuilder configuration is empty or invalid.");

    new AssetPipeline(config, Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory).Run();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"AssetBuilder failed: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}

static string ParseConfigPath(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--config" or "-c")
        {
            if (i + 1 >= args.Length) throw new ArgumentException("--config requires a file path.");
            return args[i + 1];
        }
    }
    return "config/assetbuilder.json";
}
