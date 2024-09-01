﻿using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace H2MLauncher.Core.Services;

public enum NetAddressType
{
    NA_BOT = 0x0,
    NA_BAD = 0x1,
    NA_LOOPBACK = 0x2,
    NA_BROADCAST = 0x3,
    NA_IP = 0x4,
}

public enum NetSrc
{
    NS_CLIENT1 = 0x0,
    NS_MAXCLIENTS = 0x1,
    NS_SERVER = 0x2,
    NS_PACKET = 0x3,
    NS_INVALID_NETSRC = 0x4,
}

// Struct matching netadr_s
[StructLayout(LayoutKind.Sequential)]
public struct NetAddress
{
    public NetAddressType Type;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] IP;

    public ushort Port;

    public NetSrc localNetID;

    public uint AddrHandleIndex;
}

// Struct matching connect_state_t
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ConnectState
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] // To match the __pad0[0xC]
    public byte[] __pad0;
    public NetAddress Address;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ClientState
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x4A50)]
    public byte[] __pad0;

    public int Ping;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x8)]
    public byte[] pad1;

    public int NumPlayers;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] pad2;

    public int ServerTime;
}

public enum ConnectionState
{
    CA_DISCONNECTED = 0x0,
    CA_CINEMATIC = 0x1,
    CA_LOGO = 0x2,
    CA_CONNECTING = 0x3,
    CA_CHALLENGING = 0x4,
    CA_CONNECTED = 0x5,
    CA_SENDINGSTATS = 0x6,
    CA_SYNCHRONIZING_DATA = 0x7,
    CA_LOADING = 0x8,
    CA_PRIMED = 0x9,
    CA_ACTIVE = 0xA,
}

public sealed class GameMemory : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);    


    const nint PLAYER_NAME_OFFSET_H1 = 0x3516F83;
    const nint DISCORD_ACTIVITY_OFFSET_H2MMOD = 0x56FF29;
    const nint CONNECTION_STATE_H1 = 0x2EC82C8;
    const nint LEVEL_ENTITY_ID_H1 = 0xB1100B0;
    const nint CLIENT_STATE_PTR_H1 = 0x2EC84F0;
    const nint CONNECT_STATE_PTR_H1 = 0x2EC8510;
    const nint SV_SERVERID_H1 = 0xB7F9630;
    const nint VIRTUAL_LOBBY_LOADED_H1 = 0x2E6EC9D;


    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    // Constants for process access rights
    const int PROCESS_CREATE_THREAD = 0x0002;
    const int PROCESS_QUERY_INFORMATION = 0x0400;
    const int PROCESS_VM_OPERATION = 0x0008;
    const int PROCESS_VM_WRITE = 0x0020;
    const int PROCESS_VM_READ = 0x0010;

    public Process Process { get; }

    private readonly IntPtr _processHandle;
    private readonly IntPtr _h1BaseAddress;

    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public GameMemory(Process process)
    {
        var h1Module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => m.ModuleName.Equals("h1_mp64_ship.exe"));
        if (h1Module is null)
        {
            throw new Exception("Game module not found in process");
        }

        _h1BaseAddress = h1Module.BaseAddress;

        _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
        Process = process;
    }

    public void Dispose()
    {
        CloseHandle(_processHandle);
    }

    public int GetSvServerId()
    {
        if (ReadProcessMemoryInt(_processHandle, _h1BaseAddress + SV_SERVERID_H1, out int sv_serverid))
        {
            return sv_serverid;
        }

        return 0;
    }

    public ClientState? GetClientState()
    {
        if (ReadStructFromMemoryPtr(_processHandle, _h1BaseAddress + CLIENT_STATE_PTR_H1, out ClientState? clientState))
        {
            return clientState;
        }

        return null;
    }

    public ConnectState? GetConnectState()
    {
        if (ReadStructFromMemoryPtr(_processHandle, _h1BaseAddress + CONNECT_STATE_PTR_H1, out ConnectState? connectState))
        {
            return connectState;
        }

        return null;
    }

    public ConnectionState? GetConnectionState()
    {
        if (ReadProcessMemoryInt(_processHandle, _h1BaseAddress + CONNECTION_STATE_H1, out int connectionState))
        {
            return (ConnectionState)connectionState;
        }

        return null;
    }

    public bool? GetVirtualLobbyLoaded()
    {
        if (ReadProcessMemoryBool(_processHandle, _h1BaseAddress + VIRTUAL_LOBBY_LOADED_H1, out bool loaded))
        {
            return loaded;
        }

        return null;
    }


    public static void Start(Process targetProcess)
    {
        // Define the address you want to read from
        IntPtr baseAddress = new IntPtr(
            targetProcess.Modules.Cast<ProcessModule>().First(m => m.ModuleName.Contains("h1_mp64")).BaseAddress);


        // Open the process with read access
        IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, targetProcess.Id);

        //var mem = new Memory(targetProcess.ProcessName, "h1_mp64_ship.exe", ACCESS_LEVEL.PROCESS_VM_READ | ACCESS_LEVEL.PROCESS_QUERY_INFORMATION);

        Task.Run(async () =>
        {
            //var playerNameMemorySource = new MemoryReadSourceString(8, hProcess, baseAddress + PLAYER_NAME_OFFSET_H1, Encoding.ASCII);

            while (!targetProcess.HasExited)
            {
                Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);
                await _semaphore.WaitAsync();
                try
                {
                    if (ReadProcessMemoryInt(hProcess, baseAddress + SV_SERVERID_H1, out int sv_serverid))
                    {
                        Debug.WriteLine("sv_serverId: " + sv_serverid);
                    }

                    if (ReadProcessMemoryInt(hProcess, baseAddress + CONNECTION_STATE_H1, out int connectionState))
                    {
                        Debug.WriteLine("Connection State: " + ((ConnectionState)connectionState).ToString());
                    }

                    if (ReadProcessMemoryUInt(hProcess, baseAddress + LEVEL_ENTITY_ID_H1, out uint levelEntityId))
                    {
                        Debug.WriteLine("Entity Id: " + levelEntityId);
                    }

                    if (ReadStructFromMemoryPtr(hProcess, baseAddress + CLIENT_STATE_PTR_H1, out ClientState? clientState))
                    {
                        if (clientState is not null)
                        {
                            Debug.WriteLine("Ping: " + clientState.Value.Ping);
                            Debug.WriteLine("NumPlayers: " + clientState.Value.NumPlayers);
                            Debug.WriteLine("ServerTime: " + clientState.Value.ServerTime);
                        }
                    }

                    if (ReadStructFromMemoryPtr(hProcess, baseAddress + CONNECT_STATE_PTR_H1, out ConnectState? connectState))
                    {
                        if (connectState is not null)
                        {
                            Debug.WriteLine("Type: " + connectState.Value.Address.Type.ToString());
                            Debug.WriteLine("IP: " + string.Join(".", connectState.Value.Address.IP));
                            Debug.WriteLine("Port: " + connectState.Value.Address.Port);
                            Debug.WriteLine("LocalNetID: " + connectState.Value.Address.localNetID.ToString());
                            Debug.WriteLine("AddrHandleIndex: " + connectState.Value.Address.AddrHandleIndex);
                        }
                    }


                    await Task.Delay(1000).ConfigureAwait(true);
                    //Thread.Sleep(1000);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }).ContinueWith(t =>
        {
            CloseHandle(hProcess);
        });
    }

    static bool ReadPointerFromMemory(IntPtr processHandle, IntPtr address, out nint ptrValue)
    {
        // Assume the pointer is an IntPtr (which is 4 bytes on x86 or 8 bytes on x64)
        int pointerSize = Marshal.SizeOf(typeof(IntPtr));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(pointerSize);

        try
        {
            // Read the memory containing the pointer
            if (ReadProcessMemory(processHandle, address, buffer, pointerSize, out int bytesRead))
            {
                // Convert the byte array to an IntPtr (which holds the address of the struct)
                Span<byte> span = new(buffer, 0, IntPtr.Size);
                ptrValue = MemoryMarshal.Read<IntPtr>(span);
                return true;
            }
            //else
            //{
            //    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            //}

            ptrValue = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static bool ReadStructFromMemory<T>(IntPtr processHandle, IntPtr address, out T value) where T : struct
    {
        // Get the size of the struct
        int size = Marshal.SizeOf(typeof(T));

        // Allocate a buffer for the struct
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            // Read the memory
            if (ReadProcessMemory(processHandle, address, buffer, size, out int bytesRead))
            {
                // Convert the byte array to the struct
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
                    return true;
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
            //else
            //{
            //    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            //}

            value = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static bool ReadStructFromMemoryPtr<T>(IntPtr processHandle, IntPtr address, out T? value) where T : struct
    {
        if (!ReadPointerFromMemory(processHandle, address, out nint ptrValue))
        {
            value = default;
            return false;
        }

        if (ptrValue == IntPtr.Zero)
        {
            // null pointer
            value = default;
            return true;
        }

        if (ReadStructFromMemory<T>(processHandle, ptrValue, out var val))
        {
            value = val;
            return true;
        }

        value = default;
        return false;
    }


    static bool ReadProcessMemoryInt(nint hProcess, nint lpBaseAddress, out int value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(sizeof(int));
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out _);
            if (success)
            {
                // Convert the byte array to an integer
                value = BitConverter.ToInt32(buffer);
                return true;
            }
            value = 0;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static bool ReadProcessMemoryBool(nint hProcess, nint lpBaseAddress, out bool value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(sizeof(bool));
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out _);
            if (success)
            {
                // Convert the byte array to an integer
                value = BitConverter.ToBoolean(buffer);
                return true;
            }
            value = false;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static bool ReadProcessMemoryUInt(nint hProcess, nint lpBaseAddress, out uint value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(sizeof(uint));
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out _);
            if (success)
            {
                // Convert the byte array to an integer
                value = BitConverter.ToUInt32(buffer);
                return true;
            }
            value = 0;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static bool ReadProcessMemoryString(nint hProcess, nint lpBaseAddress, int length, [MaybeNullWhen(false)] out string? value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out int bytesRead);
            if (success)
            {
                value = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                return true;
            }

            value = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static bool ReadProcessMemoryStruct<T>(nint hProcess, nint lpBaseAddress, int size, out T value) where T : struct
    {
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            if (ReadProcessMemory(hProcess, lpBaseAddress, buffer, size, out _))
            {
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {

                    // Convert the byte array into a struct                            
                    T? readValue = (T?)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                    if (readValue.HasValue)
                    {
                        value = readValue.Value;
                        return true;
                    }

                }
                finally
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }

            value = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private class MemoryReadSource
    {
        private readonly int _size;
        protected byte[] _buffer;
        private readonly IntPtr _handle;
        private readonly IntPtr _baseAddress;
        protected int _lastBytesRead;

        public ReadOnlyMemory<byte> ReadMemory => _buffer.AsMemory(0, _lastBytesRead);

        public MemoryReadSource(int size, IntPtr handle, IntPtr baseAddress)
        {
            _size = size;
            _buffer = new byte[size];
            _handle = handle;
            _baseAddress = baseAddress;
        }

        public virtual bool ReadNext()
        {
            return ReadProcessMemory(_handle, _baseAddress, _buffer, _size, out _lastBytesRead);
        }


        public bool ReadNext(out byte[] buffer)
        {
            var rbuffer = new byte[_size];
            ReadProcessMemory(_handle, _baseAddress, rbuffer, _size, out _lastBytesRead);

            buffer = rbuffer;

            return true;
        }
    }

    private class MemoryReadSourceUInt(IntPtr handle, IntPtr baseAddress) : MemoryReadSource(sizeof(uint), handle, baseAddress)
    {
        public uint Value => BitConverter.ToUInt32(ReadMemory.Span);

        public bool ReadNextValue(out uint value)
        {
            if (ReadNext())
            {
                value = BitConverter.ToUInt32(ReadMemory.Span);
                return true;
            }

            value = default;
            return false;
        }
    }

    private class MemoryReadSourceInt(IntPtr handle, IntPtr baseAddress) : MemoryReadSource(sizeof(int), handle, baseAddress)
    {
        public int Value => BitConverter.ToInt32(ReadMemory.Span);

        public bool ReadNextValue(out int value)
        {
            if (ReadNext())
            {
                value = BitConverter.ToInt32(ReadMemory.Span);
                return true;
            }

            value = default;
            return false;
        }
    }

    private class MemoryReadSourceString : MemoryReadSource
    {
        private readonly Encoding _encoding;
        public MemoryReadSourceString(int length, IntPtr handle, IntPtr baseAddress, Encoding encoding) : base(length, handle, baseAddress)
        {
            _encoding = encoding;
        }

        //public string Value => encoding.GetString(ReadMemory.Span);

        public bool ReadNextValue([MaybeNullWhen(false)] out string value)
        {
            if (ReadNext())
            {
                value = System.Text.Encoding.ASCII.GetString(_buffer, 0, _lastBytesRead);
                return true;
            }

            value = default;
            return false;
        }
    }

    private class MemoryReadSource<T> : MemoryReadSource where T : struct
    {
        public MemoryReadSource(IntPtr handle, IntPtr baseAddress) : base(Marshal.SizeOf(default(T)), handle, baseAddress)
        {

        }

        public bool ReadNextValue(out T value)
        {
            if (ReadNext())
            {
                GCHandle handle = GCHandle.Alloc(ReadMemory, GCHandleType.Pinned);
                try
                {
                    // Convert the byte array into a struct                            
                    T? readValue = (T?)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                    if (readValue.HasValue)
                    {
                        value = readValue.Value;
                        return true;
                    }
                }
                finally
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }

            value = default;
            return false;
        }
    }

    //public bool ReadLevelEntityId(out uint levelEntityId)
    //{
    //    bool success = ReadProcessMemory(_processHandle, _h1BaseAddress + LEVEL_ENTITY_ID_H1, _levelEntityIdBuffer, sizeof(uint), out _);
    //    if (success)
    //    {
    //        levelEntityId = BitConverter.ToUInt32(_levelEntityIdBuffer);
    //    }
    //}
}
