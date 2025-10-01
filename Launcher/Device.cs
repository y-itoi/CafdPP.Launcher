using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

using MadWizard.WinUSBNet;

namespace Nl.vtc
{
    public partial class Device : IDisposable
    {
        public Device( USBDeviceInfo trg )
        {
            this.DeviceInfo = trg;
        }
        ~Device()
        {
            this.Dispose();
        }
        public void Dispose()
        {
            this.FreeCache();
        }
        internal USBDeviceInfo DeviceInfo { get; }

        // ブロックサイズ
        internal const int BlockSize = 0x38;
        // コンテンツサイズ
        internal const int ContentSize = 0x08;
        // アドレスマップ
        internal struct AddressMap
        {
            public const int Keycode = 0x00;
            public const int Extensions = 0x70;
        };
        // 拡張領域オフセット
        internal struct AddressMapEx
        {
            public const int CustomerId = AddressMap.Extensions + 0x00;
            public const int SerialNo = AddressMap.Extensions + 0x08;
            public const int UniqueCode = AddressMap.Extensions + 0x10;
        };

        /// <summary>
        /// デバイスのデータ・メモリをローカル領域にマップします
        /// </summary>
        #region    Buffer
        internal byte[] Buffer {
            get {
                if (_Buffer == null) {
                    _Buffer = new byte[BlockSize * 3];

                    try {
                        using (var drv = new Driver( this.DeviceInfo )) {

                            // キーコード領域
                            {
                                int off = AddressMap.Keycode;
                                int addr_byte = off & 0xFE;
                                int addr_word = addr_byte / 2;
                                int off_byte = off - addr_byte;

                                int len = BlockSize * 2;
                                int len_byte = (len + 0x01) & 0xFE;
                                int len_word = len_byte / 2;
                                byte[] buffer = new byte[len_byte];

                                // ReadData コマンドはワードアドレスを自動インクリメントする。
                                if (drv.SetAddress( (byte)addr_word )) {
                                    for (int i = 0; i < len_word; i++) {
                                        if (!drv.ReadData( ref buffer[i * 2 + 0], ref buffer[i * 2 + 1] )) {
                                            break;
                                        }
                                    }
                                }
                                Array.Copy( buffer, 0x00, _Buffer, off, len );
                            }
                            // 拡張領域
                            {
                                int off = AddressMap.Extensions;
                                int addr_byte = off & 0xFE;
                                int addr_word = addr_byte / 2;
                                int off_byte = off - addr_byte;

                                int len = BlockSize;
                                int len_byte = (len + 0x01) & 0xFE;
                                int len_word = len_byte / 2;
                                byte[] buffer = new byte[len_byte];

                                // 拡張領域のワードアドレスは自動インクリメントしない。
                                for (int i = 0; i < len_word; i++) {

                                    if (drv.SetAddress( (byte)addr_word++ )) {
                                        if (drv.ReadData( ref buffer[i * 2 + 0], ref buffer[i * 2 + 1] )) {
                                            continue;
                                        }
                                    }
                                    break;
                                }
                                Array.Copy( buffer, 0x00, _Buffer, off, len );
                            }
                        }
                    }
                    catch (Exception ex) {
                        Console.WriteLine( ex.Message );
                    }
                }
                return _Buffer;
            }
        }
        private byte[] _Buffer = null;
        #endregion(Buffer)

        /// <summary>
        /// デバイスを識別する厳密な名前を取得します
        /// </summary>
        #region    StrongName
        public byte[] StrongName {
            get {
                if (_StrongName == null) {

                    var buffer = this.Uc;
                    if (buffer.All( ( e ) => e == 0x00 )) {

                        _StrongName = null;
                    }
                    else {

                        using var provider = MD5.Create();
                        _StrongName = provider.ComputeHash( buffer );
                    }
                }
                return _StrongName;
            }
        }
        private byte[] _StrongName = null;

        public static byte[] IntPtrAsHash( IntPtr handle )
        {
            using var provider = MD5.Create();
            byte[] buffer = new byte[provider.HashSize >> 3]; // 128bit → 16byte
            Marshal.Copy( handle, buffer, 0, buffer.Length );
            return buffer;
        }
        public IntPtr HashAsIntPtr {
            get { return this.AllocCache(); }
        }
        private IntPtr AllocCache()
        {
            if (_Cache == IntPtr.Zero && this.StrongName != null) {
                _Cache = Marshal.AllocHGlobal( this.StrongName.Length );
                Marshal.Copy( this.StrongName, 0, _Cache, this.StrongName.Length );
            }
            return _Cache;
        }
        private void FreeCache()
        {
            if (_Cache != IntPtr.Zero) {
                Marshal.FreeHGlobal( _Cache );
            }
            _Cache = IntPtr.Zero;
        }
        private IntPtr _Cache = IntPtr.Zero;
        #endregion(厳密な名前)

        /// <summary>
        /// デバイスの任意の領域を読み込みます
        /// </summary>
        /// <param name="addr">アドレス</param>
        /// <param name="length">サイズ</param>
        /// <returns>データ</returns>
        internal byte[] ReadBuffer( int addr, int length )
        {
            var result = new byte[length];

            try {
                using (var drv = new Driver( this.DeviceInfo )) {

                    int off = addr;
                    int addr_byte = off & 0xFE;
                    int addr_word = addr_byte / 2;
                    int off_byte = off - addr_byte;

                    int len_byte = (length + 0x01) & 0xFE;
                    int len_word = len_byte / 2;
                    byte[] buffer = new byte[len_byte];

                    // ReadData コマンドはワードアドレスを自動インクリメントする。
                    if (drv.SetAddress( (byte)addr_word )) {
                        for (int i = 0; i < len_word; i++) {
                            if (!drv.ReadData( ref buffer[i * 2 + 0], ref buffer[i * 2 + 1] )) {
                                break;
                            }
                        }
                    }
                    Array.Copy( buffer, off_byte, result, 0x00, length );
                }
            }
            catch (Exception ex) {
                Console.WriteLine( ex.Message );
            }
            return result;
        }

        /// <summary>
        /// カスタマーIDを取得します
        /// </summary>
        #region Id
        public byte[] Id {
            get {
                if (_id_cache == null) {
                    _id_cache = this.ReadBuffer( AddressMapEx.CustomerId, Driver.DeviceRequest.PacketSize.Pack );
                }
                return _id_cache;
            }
        }
        private byte[] _id_cache = default;
        #endregion

        /// <summary>
        /// シリアルナンバーを取得します
        /// </summary>
        #region No
        public byte[] No {
            get {
                if (_no_cache == null) {
                    _no_cache = this.ReadBuffer( AddressMapEx.SerialNo, Driver.DeviceRequest.PacketSize.Pack );
                }
                return _no_cache;
            }
        }
        private byte[] _no_cache = default;
        #endregion

        /// <summary>
        /// デバイス識別子を取得します
        /// </summary>
        #region Uc
        public byte[] Uc {
            get {
                if (_uc_cache == null) {
                    _uc_cache = this.ReadBuffer( AddressMapEx.UniqueCode, Driver.DeviceRequest.PacketSize.Pack );
                }
                return _uc_cache;
            }
        }
        private byte[] _uc_cache = default;
        #endregion
    }
}
