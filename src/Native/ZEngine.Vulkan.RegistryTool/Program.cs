using System.Text;
using ZEngine.Vulkan.Registry;

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: ZEngine.Vulkan.RegistryTool <vk.xml> <output.cs>");
    return 2;
}

string registryPath = Path.GetFullPath(args[0]);
string outputPath = Path.GetFullPath(args[1]);

await using FileStream input = File.OpenRead(registryPath);
VulkanRegistrySnapshot registry = VulkanRegistryReader.Read(input);

Directory.CreateDirectory(
    Path.GetDirectoryName(outputPath)
    ?? throw new InvalidOperationException("The output path has no directory."));

await using StreamWriter output = new(
    outputPath,
    append: false,
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

VulkanRegistryCodeWriter.Write(registry, output);

Console.WriteLine(
    $"Generated {registry.Handles.Count} handles from Vulkan {registry.HighestCoreFeature.Version} with {registry.Commands.Count} commands.");
return 0;
