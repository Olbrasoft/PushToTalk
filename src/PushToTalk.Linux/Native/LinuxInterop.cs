using System.Runtime.InteropServices;

namespace Olbrasoft.PushToTalk.Native;

/// <summary>
/// P/Invoke declarations for Linux system calls.
/// </summary>
internal static partial class LinuxInterop
{
    private const string LibC = "libc";

    // File open flags
    public const int O_RDONLY = 0;
    public const int O_WRONLY = 1;
    public const int O_RDWR = 2;
    public const int O_NONBLOCK = 2048; // 0x800

    // uinput ioctl commands
    public const uint UI_SET_EVBIT = 0x40045564;
    public const uint UI_SET_KEYBIT = 0x40045565;
    public const uint UI_DEV_CREATE = 0x5501;
    public const uint UI_DEV_DESTROY = 0x5502;

    // Event types
    public const ushort EV_SYN = 0x00;
    public const ushort EV_KEY = 0x01;

    // Sync event codes
    public const ushort SYN_REPORT = 0x00;

    // Bus types for uinput_user_dev
    public const ushort BUS_USB = 0x03;

    /// <summary>
    /// Opens a file and returns a file descriptor.
    /// </summary>
    [LibraryImport(LibC, EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string pathname, int flags);

    /// <summary>
    /// Closes a file descriptor.
    /// </summary>
    [LibraryImport(LibC, EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int fd);

    /// <summary>
    /// Writes data to a file descriptor.
    /// </summary>
    [LibraryImport(LibC, EntryPoint = "write", SetLastError = true)]
    public static partial nint Write(int fd, nint buf, nuint count);

    /// <summary>
    /// Performs an ioctl operation on a file descriptor with an integer argument.
    /// </summary>
    [LibraryImport(LibC, EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, uint request, int value);
}

/// <summary>
/// Linux input_event structure for sending keyboard events.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct InputEvent
{
    public long TimeSec;      // time_t (seconds)
    public long TimeUsec;     // suseconds_t (microseconds)
    public ushort Type;       // event type
    public ushort Code;       // event code
    public int Value;         // event value

    public static InputEvent Create(ushort type, ushort code, int value)
    {
        return new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = type,
            Code = code,
            Value = value
        };
    }
}

/// <summary>
/// Linux uinput_user_dev structure for creating virtual input devices.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UinputUserDev
{
    public fixed byte Name[80];    // Device name
    public ushort BusType;         // Bus type
    public ushort Vendor;          // Vendor ID
    public ushort Product;         // Product ID
    public ushort Version;         // Version
    public uint FfEffectsMax;      // Max force feedback effects
    public fixed int Absmax[64];   // Absolute max values
    public fixed int Absmin[64];   // Absolute min values
    public fixed int Absfuzz[64];  // Absolute fuzz values
    public fixed int Absflat[64];  // Absolute flat values

    public static UinputUserDev Create(string name, ushort vendor = 0x1234, ushort product = 0x5678)
    {
        var dev = new UinputUserDev
        {
            BusType = LinuxInterop.BUS_USB,
            Vendor = vendor,
            Product = product,
            Version = 1,
            FfEffectsMax = 0
        };

        // Copy name to fixed buffer
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var copyLen = Math.Min(nameBytes.Length, 79); // Leave room for null terminator
        for (int i = 0; i < copyLen; i++)
        {
            dev.Name[i] = nameBytes[i];
        }

        return dev;
    }
}
