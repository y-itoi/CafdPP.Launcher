/*  WinUSBNet library
 *  (C) 2010 Thomas Bleeker (www.madwizard.org)
 *
 *  Licensed under the MIT license, see license.txt or:
 *  http://www.opensource.org/licenses/mit-license.php
 */

/* NOTE: Parts of the code in this file are based on the work of Jan Axelson
 * See http://www.lvr.com/winusb.htm for more information
 */

using System;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace MadWizard.WinUSBNet.API
{
    ///  <summary>
    ///  API declarations relating to file I/O (and used by WinUsb).
    ///  </summary>
    public partial class FileIO
    {
        public const int FILE_ATTRIBUTE_NORMAL = 0X80;
        public const int FILE_FLAG_OVERLAPPED = 0X40000000;
        public const int FILE_SHARE_READ = 1;
        public const int FILE_SHARE_WRITE = 2;
        public const int GENERIC_READ = /*0x80000000*/ -2147483648;
        public const int GENERIC_WRITE = 0x40000000;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr( -1 );
        public const int OPEN_EXISTING = 3;

        public const int ERROR_IO_PENDING = 997;

        [LibraryImport( "kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16 )]
        public static partial SafeFileHandle CreateFile( string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, int hTemplateFile );
    }
}
