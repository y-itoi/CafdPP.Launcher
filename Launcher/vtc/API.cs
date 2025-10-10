using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

using MadWizard.WinUSBNet;

// PublickKeyはsn.exe(StrongName:厳密な名前ツール)を使用して取得します
// ※開発者コマンドプロンプトを利用すればsn.exeのパスが通っています
// 
// 手順1)公開キーファイルを生成します
// sn -p N-LOCK3.snk N-LOCK3.PublicKeyOnly.snk
//
// 手順2)公開キーファイルから公開キーを出力します
// sn -tp N-LOCK3.snk
// 
//[assembly: InternalsVisibleTo( "N-LOCK3, PublicKey=0024000004800000940000000602000000240000525341310004000001000100317b1e099f88fb"
//                                                + "37272b82d82397795dc5e1244fd403ad33fb8700b7e64c36261b8d519be1a649922518fc67e6d5"
//                                                + "fb17653f9ca45b177bc0de07f46f1204490c1a470a1580e6126adf0c903f7ed747f24c0bc876ea"
//                                                + "d43c927dedfcdcc34861fa4aa40baa2da38d8ab115be9f897a976a41d6058ff639af784a546735"
//                                                + "5a1583ae" )]
//[assembly: InternalsVisibleTo( "vtcx,    PublicKey=002400000480000094000000060200000024000052534131000400000100010055b79c873709fa"
//                                                + "856c0ace883ebe3c0e5d9a9e5c453c2ccd8b6930a63ab41bdafb2b30281fee8f3b1a8a1caf7d60"
//                                                + "37c89a9bf8e5a303eb385b4d3d476356717a3f390af035ee58ab038fb08b4a8e1972731d7e1453"
//                                                + "f3952b2a22cb06181c7d55a4af2b19d1c79d5fc090dcb6f692ccce8af6ee5ca882d37a0ac376d4"
//                                                + "83cf5bb8" )]

namespace Nl.vtc
{
    public static class API
    {
        static API()
        {
        //    // System.Windows.Controlクラスを作成する。
        //    API.HiddenDummy = new System.Windows.Forms.Form {
        //        ControlBox = false,
        //        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
        //        HelpButton = false,
        //        ImeMode = System.Windows.Forms.ImeMode.Off,
        //        KeyPreview = true,
        //        MaximizeBox = false,
        //        MinimizeBox = false,
        //        ShowIcon = false,
        //        ShowInTaskbar = false,
        //        Text = "N-Lock Manager",
        //        TopMost = false,
        //        WindowState = System.Windows.Forms.FormWindowState.Minimized
        //    };
        //
        //    API.DevMgr.RegistNotifier( API.HiddenDummy );
        //    API.DevMgr.Attached += OnAttached;
        //    API.DevMgr.Detached += OnDetached;
        //
        //    // Handleプロパティを取得すると、Showの実行ない(Visibleがfalse)ままでHandelCreatedを通知できる。
        //    API.HandleDummy = API.HiddenDummy.Handle;
        }
        //private static System.Windows.Forms.Form HiddenDummy = null;
        //private static IntPtr HandleDummy;

        internal static Manager DevMgr { get { return Manager.GetInstance(); } }



        ////(CheckIDのシーケンスが存在するので、Attach/Detachが通知されても仕方ない)
        //public static void OnAttached( USBEvent e )
        //{
        //    if (API.Attached != null) {
        //        API.Attached();
        //    }
        //    API.IsChecked = false;
        //}
        //public static void OnDetached( USBEvent e )
        //{
        //    if (API.Detached != null) {
        //        API.Detached();
        //    }
        //    API.IsChecked = false;
        //}
        //public static event NotifyHandler Attached;
        //public static event NotifyHandler Detached;
        //public delegate void NotifyHandler();



        /// <summary>
        /// エラーコード(公開)
        /// </summary>
        public static class ErrCode
        {
            public const int OK = 0;

            /// <summary>
            /// index が指す N-LOCK が見つからない
            /// </summary>
            public const int NotFound = -1;
            /// <summary>
            /// N-LOCKへのアクセスは許可されていない
            /// </summary>
            public const int AccessDenied = -2;
            /// <summary>
            /// CustomerID が一致しない
            /// </summary>
            public const int CheckIdMissMatched = -3;
            /// <summary>
            /// CustomerID の取得に失敗した
            /// </summary>
            public const int GetIdFailed = -4;
            /// <summary>
            /// SerialNo の取得に失敗した
            /// </summary>
            public const int GetNoFailed = -5;
            /// <summary>
            /// メモリアドレスが不正(0x00～0x7F)
            /// </summary>
            public const int InvalidAddress = -6;
            /// <summary>
            /// メモリサイズが不正
            /// </summary>
            public const int InvalidSize = -7;
            /// <summary>
            /// メモリの読み込みに失敗
            /// </summary>
            public const int GetMemFailed = -8;
        }



        /// <summary>
        /// コンピュータに接続されているN-LOCKの数を得る
        /// </summary>
        /// <param name="Count">N-LOCKの数</param>
        /// <returns>エラーコード</returns>
        public static int ScnDev( out int Count )
        {
            //Debug.Print( "ScnDev" );

            API.DevMgr.Refresh();

            Count = API.DevMgr.DeviceInfoList.Count;

            return 0 < Count ? ErrCode.OK : ErrCode.NotFound;
        }

        /// <summary>
        /// N-LOCKのカスタマーIDを照会する
        /// </summary>
        /// <param name="index">>N-LOCKの位置を指定する 1 から始まるインデックス番号</param>
        /// <param name="id">カスタマーID、8 バイト固定サイズ</param>
        /// <returns>エラーコード</returns>
        public static int CheckId( int index, [MarshalAs( UnmanagedType.LPArray, SizeConst = 8 )] byte[] id )
        {
            //Debug.Print( "CheckId({0}, \"********\")", index );

            USBDeviceInfo? i = null;
            try {

                i = API.DevMgr.DeviceInfoList.ElementAt( index - 1 );
            }
            catch (Exception ex) {

                Debug.Print( ex.Message );
            }
            finally {

                API.IsChecked = false;
                API.CheckedDevice = null;
            }
            if (i != null) {

                var nlock = new Device( i );
                if (nlock != null) {

                    var pack_l = Device.Driver.DeviceRequest.PacketSize.Pack;
                    if (id.Length != pack_l) {
                        Array.Resize( ref id, pack_l );
                    }
                    if (nlock.Id.SequenceEqual( id )) {

                        // カスタマーIDが一致した事を知らせる
                        API.IsChecked = true;
                        // このデバイス情報を保持する
                        API.CheckedDevice = nlock;

                        return ErrCode.OK;
                    }
                    if (nlock.Buffer == null) {

                        return ErrCode.AccessDenied;
                    }
                    else {

                        return ErrCode.CheckIdMissMatched;
                    }
                }
            }
            return ErrCode.NotFound;
        }
        private static bool IsChecked { get; set; }
        internal static Device? CheckedDevice { get; set; }

        /// <summary>
        /// 指定のN-LOCKを認証デバイスとして利用する事を通知して厳密な識別子を得る
        /// </summary>
        /// <param name="index">>N-LOCKの位置を指定する 1 から始まるインデックス番号</param>
        /// <param name="handle">N-LOCKの識別子</param>
        /// <returns>エラーコード</returns>
        public static int GetHandle( int index, out IntPtr handle )
        {
            Debug.Print( "GetHandle({0}, ref)", index );

            if (API.IsChecked) {

                handle = API.CheckedDevice.HashAsIntPtr;
            }
            else {

                handle = IntPtr.Zero;

                if (API.CheckedDevice != null) {
                    API.DevMgr.Refresh();
                    var i = API.DevMgr.DeviceInfoList.ElementAtOrDefault( index - 1 );
                    if (i != null) {
                        var nlock = new Device( i );
                        if (nlock.StrongName.SequenceEqual( API.CheckedDevice.StrongName )) {

                            handle = nlock.HashAsIntPtr;
                            API.IsChecked = true;
                        }
                    }
                }
                if (!API.IsChecked) {

                    return ErrCode.NotFound;
                }
            }
            return ErrCode.OK;
        }

        /// <summary>
        /// 指定された識別子が示すN-LOCKが接続されているか確認する
        /// </summary>
        /// <param name="handle">N-LOCKの識別子</param>
        /// <returns>エラーコード</returns>
        public static int Authenticate( IntPtr handle )
        {
            Debug.Print( "Authenticate(0x{0})", handle.ToString( "X08" ) );

            var val = Device.IntPtrAsHash( handle );
            if (API.IsChecked) {

                if (!API.CheckedDevice.StrongName.SequenceEqual( val )) {

                    return ErrCode.NotFound;
                }
            }
            else {

                if (API.CheckedDevice != null) {
                    var sn = API.CheckedDevice.StrongName;

                    API.DevMgr.Refresh();
                    foreach (var i in API.DevMgr.DeviceInfoList) {

                        var nlock = new Device( i );
                        if (nlock.StrongName.SequenceEqual( sn )) {

                            API.IsChecked = sn.SequenceEqual( val );
                            break;
                        }
                    }
                }
                if (!API.IsChecked) {

                    return ErrCode.NotFound;
                }
            }
            return ErrCode.OK;
        }

        #region 古いバージョン互換
        /// <summary>
        /// N-LOCKが接続されているか確認する
        /// </summary>
        /// <param name="index">N-LOCKの位置を指定する 1 から始まるインデックス番号</param>
        /// <returns>エラーコード</returns>
        public static int GetStatus( int index )
        {
            Debug.Print( "GetStatus({0})", index );

            if (API.IsChecked) {

            }
            else {

                if (API.CheckedDevice != null) {
                    API.DevMgr.Refresh();
                    var i = API.DevMgr.DeviceInfoList.ElementAtOrDefault( index - 1 );
                    if (i != null) {
                        var nlock = new Device( i );
                        if (nlock.StrongName.SequenceEqual( API.CheckedDevice.StrongName )) {

                            API.IsChecked = true;
                        }
                    }
                }
                if (!API.IsChecked) {

                    return ErrCode.NotFound;
                }
            }
            return ErrCode.OK;
        }

        /// <summary>
        /// カスタマーIDを取得する
        /// </summary>
        /// <param name="index">N-LOCKの位置を指定する 1 から始まるインデックス番号</param>
        /// <param name="id">カスタマーIDを格納するバッファ、8 バイト固定サイズ</param>
        /// <returns>エラーコード</returns>
        public static int GetId( int index, IntPtr id )
        {
            Debug.Print( "GetId({0}, 0x{1})", index, id.ToString( "X08" ) );

            byte[] val = null;
            if (API.IsChecked) {

                val = API.CheckedDevice.Id;
            }
            else {

                if (API.CheckedDevice != null) {
                    API.DevMgr.Refresh();
                    var i = API.DevMgr.DeviceInfoList.ElementAtOrDefault( index - 1 );
                    if (i != null) {
                        var nlock = new Device( i );
                        if (nlock.StrongName.SequenceEqual( API.CheckedDevice.StrongName )) {

                            val = nlock.Id;
                            API.IsChecked = true;
                        }
                    }
                }
                if (!API.IsChecked) {

                    return ErrCode.NotFound;
                }
            }
            Marshal.Copy( val, 0, id, val.Length );

            return ErrCode.OK;
        }

        /// <summary>
        /// シリアルナンバーを取得する
        /// </summary>
        /// <param name="index">N-LOCKの位置を指定する 1 から始まるインデックス番号</param>
        /// <param name="no">シリアルナンバーを格納するバッファ、8 バイト固定サイズ</param>
        /// <returns>エラーコード</returns>
        public static int GetNo( int index, IntPtr no )
        {
            Debug.Print( "GetNo({0}, 0x{1})", index, no.ToString( "X08" ) );

            byte[] val = null;
            if (API.IsChecked) {

                val = API.CheckedDevice.No;
            }
            else {

                if (API.CheckedDevice != null) {
                    API.DevMgr.Refresh();
                    var i = API.DevMgr.DeviceInfoList.ElementAtOrDefault( index - 1 );
                    if (i != null) {
                        var nlock = new Device( i );
                        if (nlock.StrongName.SequenceEqual( API.CheckedDevice.StrongName )) {

                            val = nlock.No;
                            API.IsChecked = true;
                        }
                    }
                }
                if (!API.IsChecked) {

                    return ErrCode.NotFound;
                }
            }
            Marshal.Copy( val, 0, no, val.Length );

            return ErrCode.OK;
        }

        /// <summary>
        /// メモリを取得する
        /// </summary>
        /// <param name="index">N-LOCKの位置を指定する 1 から始まるインデックス番号</param>
        /// <param name="addr">アドレス</param>
        /// <param name="len">バイト数</param>
        /// <param name="ptr_buffer">データを格納するポインタまたはハンドル</param>
        /// <returns>エラーコード</returns>
        public static int GetMem( int index, int addr, int len, IntPtr ptr_buffer )
        {
            Debug.Print( "GetMem({0}, 0x{1:X04}, {2}, 0x{3})", index, addr, len, ptr_buffer.ToString( "X08" ) );

            int retcd = GetMem( index, addr, len, out byte[] val );
            if (retcd == ErrCode.OK) {

                Marshal.Copy( val, 0, ptr_buffer, len );
            }
            return retcd;
        }
        public static int GetMem( int index, int addr, int len, out byte[] val )
        {
            Debug.Print( "GetMem({0}, 0x{1:X04}, {2})", index, addr, len );

            val = [];

            if (API.IsChecked) {

                val = API.CheckedDevice.ReadBuffer( addr, len );
            }
            else {

                if (API.CheckedDevice != null) {
                    API.DevMgr.Refresh();
                    var i = API.DevMgr.DeviceInfoList.ElementAtOrDefault( index - 1 );
                    if (i != null) {
                        var nlock = new Device( i );
                        if (nlock.StrongName.SequenceEqual( API.CheckedDevice.StrongName )) {

                            val = nlock.ReadBuffer( addr, len );
                            API.IsChecked = true;
                        }
                    }
                }
                if (!API.IsChecked) {

                    return ErrCode.NotFound;
                }
            }

            return ErrCode.OK;
        }
        #endregion //(バージョン互換)
    }
}
