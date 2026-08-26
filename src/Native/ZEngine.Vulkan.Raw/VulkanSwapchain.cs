using ZEngine.Vulkan.Raw.Generated;

namespace ZEngine.Vulkan.Raw;

public sealed unsafe class VulkanSwapchain : IDisposable
{
    private readonly VkDevice _device;
    private readonly delegate* unmanaged<VkDevice, VkSwapchainKHR, void*, void>
        _destroySwapchain;
    private VkSwapchainKHR _handle;

    private VulkanSwapchain(
        VkDevice device,
        VkSwapchainKHR handle,
        VkExtent2D extent,
        VkSurfaceFormatKhr surfaceFormat,
        VkPresentModeKhr presentMode,
        IReadOnlyList<VkImage> images,
        delegate* unmanaged<VkDevice, VkSwapchainKHR, void*, void> destroySwapchain)
    {
        _device = device;
        _handle = handle;
        Extent = extent;
        SurfaceFormat = surfaceFormat;
        PresentMode = presentMode;
        Images = images;
        _destroySwapchain = destroySwapchain;
    }

    public VkSwapchainKHR Handle => _handle;

    public VkExtent2D Extent { get; }

    public VkSurfaceFormatKhr SurfaceFormat { get; }

    public VkPresentModeKhr PresentMode { get; }

    public IReadOnlyList<VkImage> Images { get; }

    internal static VulkanSwapchain Create(
        VulkanDevice device,
        VulkanSurface surface,
        VulkanSurfaceSupport support,
        uint requestedWidth,
        uint requestedHeight)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(support);

        VkSurfaceFormatKhr surfaceFormat = SelectSurfaceFormat(
            support.Formats);
        VkPresentModeKhr presentMode = support.PresentModes.Contains(
            VkPresentModeKhr.Mailbox)
            ? VkPresentModeKhr.Mailbox
            : VkPresentModeKhr.Fifo;
        VkExtent2D extent = SelectExtent(
            support.Capabilities,
            requestedWidth,
            requestedHeight);

        uint imageCount = support.Capabilities.MinImageCount + 1;
        if (support.Capabilities.MaxImageCount > 0
            && imageCount > support.Capabilities.MaxImageCount)
        {
            imageCount = support.Capabilities.MaxImageCount;
        }

        VkSwapchainCreateInfoKhr createInfo = new()
        {
            SType = VkStructureType.SwapchainCreateInfoKhr,
            Surface = surface.Handle,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = VkImageUsageFlags.ColorAttachment,
            ImageSharingMode = VkSharingMode.Exclusive,
            PreTransform = support.Capabilities.CurrentTransform,
            CompositeAlpha = SelectCompositeAlpha(
                support.Capabilities.SupportedCompositeAlpha),
            PresentMode = presentMode,
            Clipped = 1
        };

        var createSwapchain =
            (delegate* unmanaged<VkDevice, VkSwapchainCreateInfoKhr*, void*, VkSwapchainKHR*, VkResult>)device.GetProcAddress(
                "vkCreateSwapchainKHR\0"u8);

        VkSwapchainKHR handle = default;
        VulkanException.ThrowIfFailed(
            createSwapchain(
                device.Handle,
                &createInfo,
                null,
                &handle),
            "vkCreateSwapchainKHR");

        var getImages =
            (delegate* unmanaged<VkDevice, VkSwapchainKHR, uint*, VkImage*, VkResult>)device.GetProcAddress(
                "vkGetSwapchainImagesKHR\0"u8);
        var destroySwapchain =
            (delegate* unmanaged<VkDevice, VkSwapchainKHR, void*, void>)device.GetProcAddress(
                "vkDestroySwapchainKHR\0"u8);

        uint actualImageCount = 0;
        VulkanException.ThrowIfFailed(
            getImages(
                device.Handle,
                handle,
                &actualImageCount,
                null),
            "vkGetSwapchainImagesKHR(count)");

        VkImage[] images = new VkImage[actualImageCount];
        fixed (VkImage* pointer = images)
        {
            VulkanException.ThrowIfFailedOrIncomplete(
                getImages(
                    device.Handle,
                    handle,
                    &actualImageCount,
                    pointer),
                "vkGetSwapchainImagesKHR(values)");
        }

        return new(
            device.Handle,
            handle,
            extent,
            surfaceFormat,
            presentMode,
            images.AsSpan(0, (int)actualImageCount).ToArray(),
            destroySwapchain);
    }

    public void Dispose()
    {
        VkSwapchainKHR handle = _handle;
        if (!handle.IsNull)
        {
            _handle = default;
            _destroySwapchain(_device, handle, null);
        }
    }

    private static VkSurfaceFormatKhr SelectSurfaceFormat(
        IReadOnlyList<VkSurfaceFormatKhr> formats)
    {
        if (formats.Count == 0)
        {
            throw new InvalidOperationException(
                "The Vulkan surface exposes no formats.");
        }

        if (formats.Count == 1
            && formats[0].Format == VkFormat.Undefined)
        {
            return new()
            {
                Format = VkFormat.B8G8R8A8Srgb,
                ColorSpace = VkColorSpaceKhr.SrgbNonlinear
            };
        }

        return formats.FirstOrDefault(
                   format => format.Format == VkFormat.B8G8R8A8Srgb
                             && format.ColorSpace
                             == VkColorSpaceKhr.SrgbNonlinear)
               is { Format: not VkFormat.Undefined } preferred
            ? preferred
            : formats[0];
    }

    private static VkExtent2D SelectExtent(
        VkSurfaceCapabilitiesKhr capabilities,
        uint requestedWidth,
        uint requestedHeight)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }

        return new()
        {
            Width = Math.Clamp(
                requestedWidth,
                capabilities.MinImageExtent.Width,
                capabilities.MaxImageExtent.Width),
            Height = Math.Clamp(
                requestedHeight,
                capabilities.MinImageExtent.Height,
                capabilities.MaxImageExtent.Height)
        };
    }

    private static VkCompositeAlphaFlagsKhr SelectCompositeAlpha(
        VkCompositeAlphaFlagsKhr supported)
    {
        VkCompositeAlphaFlagsKhr[] preferences =
        [
            VkCompositeAlphaFlagsKhr.Opaque,
            VkCompositeAlphaFlagsKhr.PreMultiplied,
            VkCompositeAlphaFlagsKhr.PostMultiplied,
            VkCompositeAlphaFlagsKhr.Inherit
        ];

        foreach (VkCompositeAlphaFlagsKhr preference in preferences)
        {
            if ((supported & preference) != 0)
            {
                return preference;
            }
        }

        throw new InvalidOperationException(
            "The Vulkan surface exposes no supported composite alpha mode.");
    }
}
