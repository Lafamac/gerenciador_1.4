using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorColheita
{
    internal sealed class HidDevice : IDisposable
    {
        private const int HidPayloadSize = 8;
        private const int WindowsReportSize = HidPayloadSize + 1;
        private readonly FileStream stream;

        private HidDevice(FileStream stream)
        {
            this.stream = stream;
        }

        public static HidDevice Open(ushort vendorId, ushort productId)
        {
            string identity = string.Format(
                "vid_{0:x4}&pid_{1:x4}", vendorId, productId);

            foreach (string path in EnumeratePaths())
            {
                if (path.IndexOf(identity, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                try
                {
                    SafeFileHandle handle = CreateFile(
                        path,
                        GenericRead | GenericWrite,
                        FileShareRead | FileShareWrite,
                        IntPtr.Zero,
                        OpenExisting,
                        FileFlagOverlapped,
                        IntPtr.Zero);

                    if (handle.IsInvalid)
                    {
                        handle.Dispose();
                        continue;
                    }

                    try
                    {
                        FileStream deviceStream = new FileStream(
                            handle,
                            FileAccess.ReadWrite,
                            WindowsReportSize,
                            true);

                        return new HidDevice(deviceStream);
                    }
                    catch
                    {
                        handle.Dispose();
                        throw;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
                catch (Win32Exception)
                {
                }
            }

            throw new InvalidOperationException(
                "Gerenciador nao encontrado. Conecte o equipamento e tente novamente.");
        }

        public void WriteCommand(byte command)
        {
            byte[] report = new byte[WindowsReportSize];
            report[0] = 0;
            report[1] = command;

            try
            {
                stream.Write(report, 0, report.Length);
                stream.Flush();
            }
            catch (Exception error)
            {
                throw new IOException(
                    string.Format(
                        "Nao foi possivel enviar o comando USB {0}. {1}",
                        command,
                        error.Message),
                    error);
            }
        }

        public byte[] ReadPayload(int timeoutMilliseconds)
        {
            byte[] report = new byte[WindowsReportSize];
            IAsyncResult operation = stream.BeginRead(report, 0, report.Length, null, null);

            if (!operation.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
            {
                CancelPendingRead();
                try
                {
                    stream.EndRead(operation);
                }
                catch
                {
                }

                throw new TimeoutException("Tempo esgotado aguardando pacote USB.");
            }

            int received = stream.EndRead(operation);
            byte[] payload = new byte[HidPayloadSize];

            if (received == WindowsReportSize)
            {
                Buffer.BlockCopy(report, 1, payload, 0, HidPayloadSize);
            }
            else if (received == HidPayloadSize)
            {
                Buffer.BlockCopy(report, 0, payload, 0, HidPayloadSize);
            }
            else
            {
                throw new IOException(
                    string.Format("Relatorio HID com tamanho invalido: {0} bytes.", received));
            }

            return payload;
        }

        private void CancelPendingRead()
        {
            try
            {
                CancelIoEx(stream.SafeFileHandle, IntPtr.Zero);
            }
            catch (EntryPointNotFoundException)
            {
                // Windows XP does not provide CancelIoEx.
                CancelIo(stream.SafeFileHandle);
            }
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        private static IEnumerable<string> EnumeratePaths()
        {
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr deviceInfo = SetupDiGetClassDevs(
                ref hidGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                DigcfPresent | DigcfDeviceInterface);

            if (deviceInfo == InvalidHandleValue)
                yield break;

            try
            {
                uint index = 0;
                while (true)
                {
                    SpDeviceInterfaceData interfaceData = new SpDeviceInterfaceData();
                    interfaceData.Size = Marshal.SizeOf(interfaceData);

                    if (!SetupDiEnumDeviceInterfaces(
                        deviceInfo, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ErrorNoMoreItems)
                            yield break;

                        throw new Win32Exception(error);
                    }

                    uint requiredSize;
                    SetupDiGetDeviceInterfaceDetail(
                        deviceInfo,
                        ref interfaceData,
                        IntPtr.Zero,
                        0,
                        out requiredSize,
                        IntPtr.Zero);

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                        if (!SetupDiGetDeviceInterfaceDetail(
                            deviceInfo,
                            ref interfaceData,
                            detailBuffer,
                            requiredSize,
                            out requiredSize,
                            IntPtr.Zero))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        IntPtr pathPointer = new IntPtr(detailBuffer.ToInt64() + 4);
                        string path = Marshal.PtrToStringAuto(pathPointer);
                        if (!string.IsNullOrEmpty(path))
                            yield return path;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }

                    index++;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfo);
            }
        }

        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfDeviceInterface = 0x00000010;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;
        private const int ErrorNoMoreItems = 259;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            public int Size;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr parent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIo(SafeFileHandle handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);
    }
}
