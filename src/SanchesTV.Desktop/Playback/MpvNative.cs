using System.Runtime.InteropServices;

namespace SanchesTV.Desktop.Playback;

internal enum MpvEventId
{
    None = 0,
    Shutdown = 1,
    LogMessage = 2,
    GetPropertyReply = 3,
    SetPropertyReply = 4,
    CommandReply = 5,
    StartFile = 6,
    EndFile = 7,
    FileLoaded = 8,
    ClientMessage = 16,
    VideoReconfig = 17,
    AudioReconfig = 18,
    Seek = 20,
    PlaybackRestart = 21,
    PropertyChange = 22,
    QueueOverflow = 24,
    Hook = 25
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEvent
{
    public MpvEventId EventId;
    public int Error;
    public ulong ReplyUserdata;
    public IntPtr Data;
}

internal static class MpvNative
{
    internal const string DllName = "libmpv-2.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr mpv_create();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_initialize(IntPtr context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void mpv_terminate_destroy(IntPtr context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_set_option_string(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_set_property_string(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr mpv_get_property_string(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int mpv_command_string(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string command);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr mpv_wait_event(IntPtr context, double timeout);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void mpv_free(IntPtr data);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong mpv_client_api_version();

    internal static string? GetPropertyString(IntPtr context, string name)
    {
        var ptr = mpv_get_property_string(context, name);
        if (ptr == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            mpv_free(ptr);
        }
    }

    internal static bool TrySetOption(IntPtr context, string name, string value) =>
        mpv_set_option_string(context, name, value) >= 0;

    internal static bool TrySetProperty(IntPtr context, string name, string value) =>
        mpv_set_property_string(context, name, value) >= 0;

    internal static bool TryCommand(IntPtr context, string command) =>
        mpv_command_string(context, command) >= 0;

    internal static string EscapeCommandArgument(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);

    internal static string SelfTest()
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, DllName)))
            throw new FileNotFoundException("libmpv não foi encontrado no diretório do aplicativo.", DllName);

        var context = mpv_create();
        if (context == IntPtr.Zero)
            throw new InvalidOperationException("mpv_create retornou ponteiro nulo.");

        try
        {
            TrySetOption(context, "vo", "null");
            TrySetOption(context, "ao", "null");
            TrySetOption(context, "terminal", "no");

            var init = mpv_initialize(context);
            if (init < 0)
                throw new InvalidOperationException($"mpv_initialize falhou: {init}");

            return GetPropertyString(context, "mpv-version") ?? "libmpv";
        }
        finally
        {
            mpv_terminate_destroy(context);
        }
    }
}
