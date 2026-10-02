using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System;
using Microsoft.Win32;

namespace JemExtensions
{
    public class SymLinkHelper
    {
        [DllImport("kernel32.dll")]
        public static extern bool CreateSymbolicLink(
        string lpSymlinkFileName, string lpTargetFileName, SymbolicLinkType dwFlags);
        
        public enum SymbolicLinkType
        {
            File = 0,
            Directory = 1,
            AllowUnprivilegedCreate = 2
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, byte[] lpInBuffer, int nInBufferSize,
        IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
        private const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;
        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;

        /// <summary>
        /// Creates a symbolic link, attempting unprivileged creation first if Developer Mode is enabled
        /// </summary>
        public static bool CreateSymbolicLinkSafe(string linkPath, string targetPath, SymbolicLinkType linkType)
        {
            // First try with unprivileged flag if Developer Mode is enabled
            if (IsDeveloperModeEnabled())
            {
                var unprivilegedFlags = linkType | SymbolicLinkType.AllowUnprivilegedCreate;
                if (CreateSymbolicLink(linkPath, targetPath, unprivilegedFlags))
                {
                    return true;
                }
            }

            // Fallback to traditional method (requires admin if Developer Mode is not enabled)
            if (CreateSymbolicLink(linkPath, targetPath, linkType))
            {
                return true;
            }

            // Last resort for directories: a junction, which needs neither admin nor Developer Mode
            return linkType.HasFlag(SymbolicLinkType.Directory) && CreateJunction(linkPath, targetPath);
        }

        /// <summary>
        /// Creates a directory junction (mount point) at linkPath pointing to targetPath.
        /// Unlike symbolic links, junctions can be created by any user. The target must be on a local volume.
        /// </summary>
        public static bool CreateJunction(string linkPath, string targetPath)
        {
            string fullTargetPath = Path.GetFullPath(targetPath);
            if (fullTargetPath.StartsWith(@"\\"))
            {
                Trace.WriteLine($"Cannot create junction to network path: {fullTargetPath}");
                return false;
            }
            if (Directory.Exists(linkPath) || File.Exists(linkPath))
            {
                Trace.WriteLine($"Cannot create junction, path already exists: {linkPath}");
                return false;
            }

            Directory.CreateDirectory(linkPath);
            try
            {
                // REPARSE_DATA_BUFFER for a mount point: header, then the substitute and print names, each null terminated
                byte[] substituteName = Encoding.Unicode.GetBytes(@"\??\" + fullTargetPath);
                byte[] printName = Encoding.Unicode.GetBytes(fullTargetPath);
                int reparseDataLength = 8 + substituteName.Length + 2 + printName.Length + 2;
                byte[] buffer = new byte[8 + reparseDataLength];
                BitConverter.GetBytes(IO_REPARSE_TAG_MOUNT_POINT).CopyTo(buffer, 0);
                BitConverter.GetBytes((ushort)reparseDataLength).CopyTo(buffer, 4);
                BitConverter.GetBytes((ushort)0).CopyTo(buffer, 8);
                BitConverter.GetBytes((ushort)substituteName.Length).CopyTo(buffer, 10);
                BitConverter.GetBytes((ushort)(substituteName.Length + 2)).CopyTo(buffer, 12);
                BitConverter.GetBytes((ushort)printName.Length).CopyTo(buffer, 14);
                substituteName.CopyTo(buffer, 16);
                printName.CopyTo(buffer, 16 + substituteName.Length + 2);

                using (SafeFileHandle handle = CreateFile(linkPath, GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero))
                {
                    if (!handle.IsInvalid &&
                        DeviceIoControl(handle, FSCTL_SET_REPARSE_POINT, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    {
                        Trace.WriteLine($"Created junction {linkPath} -> {fullTargetPath}");
                        return true;
                    }
                    Trace.WriteLine($"Failed to create junction {linkPath} -> {fullTargetPath} (error {Marshal.GetLastWin32Error()})");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to create junction {linkPath} -> {fullTargetPath}: {ex.Message}");
            }

            // Remove the empty placeholder directory so a later attempt starts clean
            try { Directory.Delete(linkPath); } catch { }
            return false;
        }

        /// <summary>
        /// Checks if Windows Developer Mode is enabled
        /// </summary>
        private static bool IsDeveloperModeEnabled()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                {
                    if (key?.GetValue("AllowDevelopmentWithoutDevLicense") is int value)
                        return value == 1;
                }
            }
            catch
            {
                // If we can't read the registry, assume Developer Mode is not enabled
            }
            return false;
        }
    }
}
