using System.Text;
using Xunit;

namespace ZEngine.Vulkan.Registry.Tests;

public sealed class VulkanRegistryReaderTests
{
    [Fact]
    public void ReadsPinnedKhronosRegistry()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Registry",
            "vk.xml");

        using FileStream stream = File.OpenRead(path);
        VulkanRegistrySnapshot registry = VulkanRegistryReader.Read(stream);

        Assert.Equal(new Version(1, 4), registry.HighestCoreFeature.Version);
        Assert.Equal(63, registry.Handles.Count);
        Assert.Equal(865, registry.Commands.Count);
        Assert.Contains(
            registry.Commands,
            command => command.Name == "vkGetInstanceProcAddr"
                       && command.Scope == VulkanCommandScope.Instance);
        Assert.Contains(
            registry.Commands,
            command => command.Name == "vkCmdDraw"
                       && command.Scope == VulkanCommandScope.Device);
    }

    [Fact]
    public void ReadsCoreFeaturesHandlesAndCommandScopes()
    {
        const string xml =
            """
            <registry>
              <types>
                <type category="handle"><type>VK_DEFINE_HANDLE</type>(<name>VkInstance</name>)</type>
                <type category="handle" parent="VkInstance"><type>VK_DEFINE_HANDLE</type>(<name>VkPhysicalDevice</name>)</type>
                <type category="handle"><type>VK_DEFINE_NON_DISPATCHABLE_HANDLE</type>(<name>VkBuffer</name>)</type>
                <type category="handle" name="VkBufferKHR" alias="VkBuffer" />
              </types>
              <commands>
                <command>
                  <proto><type>VkResult</type> <name>vkCreateInstance</name></proto>
                  <param><type>VkInstanceCreateInfo</type>* <name>pCreateInfo</name></param>
                </command>
                <command>
                  <proto><type>void</type> <name>vkDestroyInstance</name></proto>
                  <param><type>VkInstance</type> <name>instance</name></param>
                </command>
                <command>
                  <proto><type>void</type> <name>vkCmdDraw</name></proto>
                  <param><type>VkCommandBuffer</type> <name>commandBuffer</name></param>
                </command>
                <command name="vkCmdDrawAlias" alias="vkCmdDraw" />
              </commands>
              <feature api="vulkan" name="VK_VERSION_1_0" number="1.0" />
              <feature api="vulkan" name="VK_VERSION_1_4" number="1.4" />
            </registry>
            """;

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        VulkanRegistrySnapshot registry = VulkanRegistryReader.Read(stream);

        Assert.Equal(new Version(1, 4), registry.HighestCoreFeature.Version);
        Assert.Equal(4, registry.Handles.Count);
        Assert.True(registry.Handles.Single(handle => handle.Name == "VkInstance").IsDispatchable);
        Assert.False(registry.Handles.Single(handle => handle.Name == "VkBuffer").IsDispatchable);
        Assert.Equal(
            "VkBuffer",
            registry.Handles.Single(handle => handle.Name == "VkBufferKHR").Alias);
        Assert.Equal(
            VulkanCommandScope.Instance,
            registry.Commands.Single(command => command.Name == "vkDestroyInstance").Scope);
        Assert.Equal(
            VulkanCommandScope.Device,
            registry.Commands.Single(command => command.Name == "vkCmdDraw").Scope);
        Assert.Equal(
            "vkCmdDraw",
            registry.Commands.Single(command => command.Name == "vkCmdDrawAlias").Alias);
    }
}
