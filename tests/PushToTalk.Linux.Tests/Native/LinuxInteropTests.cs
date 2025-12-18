using System.Runtime.InteropServices;
using Olbrasoft.PushToTalk.Native;

namespace Olbrasoft.PushToTalk.Linux.Tests.Native;

public class LinuxInteropTests
{
    [Fact]
    public void InputEvent_Create_ShouldSetCorrectValues()
    {
        // Arrange
        ushort type = 1;
        ushort code = 30;
        int value = 1;

        // Act
        var ev = InputEvent.Create(type, code, value);

        // Assert
        Assert.Equal(0, ev.TimeSec);
        Assert.Equal(0, ev.TimeUsec);
        Assert.Equal(type, ev.Type);
        Assert.Equal(code, ev.Code);
        Assert.Equal(value, ev.Value);
    }

    [Theory]
    [InlineData(LinuxInterop.EV_KEY, (ushort)30, 1)] // Key press
    [InlineData(LinuxInterop.EV_KEY, (ushort)30, 0)] // Key release
    [InlineData(LinuxInterop.EV_SYN, LinuxInterop.SYN_REPORT, 0)] // Sync
    public void InputEvent_Create_VariousEventTypes_ShouldWork(ushort type, ushort code, int value)
    {
        // Act
        var ev = InputEvent.Create(type, code, value);

        // Assert
        Assert.Equal(type, ev.Type);
        Assert.Equal(code, ev.Code);
        Assert.Equal(value, ev.Value);
    }

    [Fact]
    public unsafe void InputEvent_Size_ShouldBe24Bytes()
    {
        // input_event struct should be 24 bytes on 64-bit systems
        // (8 bytes timeval_sec + 8 bytes timeval_usec + 2 bytes type + 2 bytes code + 4 bytes value)
        Assert.Equal(24, sizeof(InputEvent));
    }

    [Fact]
    public void UinputUserDev_Create_ShouldSetCorrectValues()
    {
        // Arrange
        var name = "test-device";

        // Act
        var dev = UinputUserDev.Create(name);

        // Assert
        Assert.Equal(LinuxInterop.BUS_USB, dev.BusType);
        Assert.Equal((ushort)0x1234, dev.Vendor);
        Assert.Equal((ushort)0x5678, dev.Product);
        Assert.Equal((ushort)1, dev.Version);
        Assert.Equal(0u, dev.FfEffectsMax);
    }

    [Fact]
    public unsafe void UinputUserDev_Create_ShouldCopyNameCorrectly()
    {
        // Arrange
        var name = "test-kbd";

        // Act
        var dev = UinputUserDev.Create(name);

        // Assert - verify name was copied
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        for (int i = 0; i < nameBytes.Length; i++)
        {
            Assert.Equal(nameBytes[i], dev.Name[i]);
        }
        // Verify null terminator
        Assert.Equal(0, dev.Name[nameBytes.Length]);
    }

    [Fact]
    public unsafe void UinputUserDev_Create_WithLongName_ShouldTruncate()
    {
        // Arrange - name longer than 79 chars
        var name = new string('x', 100);

        // Act
        var dev = UinputUserDev.Create(name);

        // Assert - should be truncated at 79 chars
        for (int i = 0; i < 79; i++)
        {
            Assert.Equal((byte)'x', dev.Name[i]);
        }
    }

    [Fact]
    public void LinuxInterop_Constants_ShouldHaveCorrectValues()
    {
        // Assert file flags
        Assert.Equal(0, LinuxInterop.O_RDONLY);
        Assert.Equal(1, LinuxInterop.O_WRONLY);
        Assert.Equal(2, LinuxInterop.O_RDWR);
        Assert.Equal(2048, LinuxInterop.O_NONBLOCK);

        // Assert uinput ioctls
        Assert.Equal(0x40045564u, LinuxInterop.UI_SET_EVBIT);
        Assert.Equal(0x40045565u, LinuxInterop.UI_SET_KEYBIT);
        Assert.Equal(0x5501u, LinuxInterop.UI_DEV_CREATE);
        Assert.Equal(0x5502u, LinuxInterop.UI_DEV_DESTROY);

        // Assert event types
        Assert.Equal((ushort)0x00, LinuxInterop.EV_SYN);
        Assert.Equal((ushort)0x01, LinuxInterop.EV_KEY);
        Assert.Equal((ushort)0x00, LinuxInterop.SYN_REPORT);

        // Assert bus type
        Assert.Equal((ushort)0x03, LinuxInterop.BUS_USB);
    }
}
