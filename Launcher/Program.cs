using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using static Nl.vtc.API;

Console.WriteLine( "(C) 2025 NEWLY CORPORATION" );
Console.WriteLine( "CAFD Plus+" );
Console.WriteLine( "" );
Console.WriteLine( " Launching program..." );

byte[] custumerId = Encoding.ASCII.GetBytes( "Newly"/*.PadRight( 8, '\0' )*/ );
byte[] versionASCII;
byte[] variationFlags = null!;
IntPtr lastCorrect = IntPtr.Zero;
Func<string, string> _MessageParser = _ => "";








int c = 0;
int retcd = ErrCode.OK;

int PulseHandle( out IntPtr hCorrect )
{
    hCorrect = IntPtr.Zero;

    retcd = ScnDev( ref c );
    if (retcd == ErrCode.OK) {
        //Console.WriteLine( $"ScnDev find {c} devices!!" );

        for (int i = 0; i < c; i++) {
            var index = i + 1;

            retcd = CheckId( index, custumerId );
            if (retcd == ErrCode.OK) {

                var handle = IntPtr.Zero;
                retcd = GetHandle( index, ref handle );
                if (retcd == ErrCode.OK) {
                    //Console.WriteLine( $"USB dongle found: {handle:X016}" );

                    // 認証
                    retcd = GetMem( index, 0x00, 8, out byte[] regist );
                    if (retcd == ErrCode.OK) {

                        if (0 != (regist[1] & 0x80)) {

                            // バージョン情報（取得のみ）
                            retcd = GetMem( index, 0x68, 8, out versionASCII );
                            if (retcd == ErrCode.OK) {

                                // バリエーション・フラグ（取得のみ）
                                retcd = GetMem( index, 0x08, 8, out variationFlags );
                                if (retcd == ErrCode.OK) {

                                    // ver[0]: 'V'
                                    // ver[1]: 'e'
                                    // ver[2]: 'r'
                                    // ver[3]: '.'
                                    var majorNumber = Encoding.ASCII.GetString( versionASCII, 4, 1 );
                                    if (int.TryParse( majorNumber, out int major )) {

                                        var minorNumber = Encoding.ASCII.GetString( versionASCII, 6, 2 );
                                        if (int.TryParse( minorNumber, out int minor )) {

                                            if (300 <= (major * 100 + minor)) {

                                                hCorrect = lastCorrect = handle;
                                                return ErrCode.OK;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        retcd = ErrCode.NotFound;
                        //Console.WriteLine( $"CAFD Plus+ don't REGISTERD!!" );

                    }
                    else {
                        //Console.WriteLine( $"GetMem failure by ({retcd})!!" );

                    }
                }
                else {
                    //Console.WriteLine( $"GetHandle failure by ({retcd})!!" );

                }
            }
            else {
                //Console.WriteLine( $"CheckId failure by ({retcd})!!" );

            }
        }
    }
    else {
        //Console.WriteLine( "Divce not found!!" );

    }
    return retcd;
}

retcd = PulseHandle( out IntPtr handle );
if (retcd == ErrCode.OK) {
    Console.WriteLine( " Program running..." );

    // 画面は表示しなくてよい
    Min();

    //「CAFD Plus+」のプロセスは監視する
    const string app = "CafdPP";
    const string exe = $"{app}.exe";
    Process? CafdPP = null;

    // メッセージハンドリング
    _MessageParser = ( cmd ) => {
        switch (cmd) {
            case "Authenticate": {

                retcd = PulseHandle( out IntPtr hCurrent );
                if (retcd == ErrCode.OK) {

                    retcd = Authenticate( hCurrent );
                    if (retcd == ErrCode.OK) {

                        return "OK";
                    }
                }
                return "NG";
            }
            case "Variation": {
                List<string> flags = [];

                if (0 != (variationFlags[0] & 0x01)) {
                    // マウントリストエディタ
                    flags.Add( "/MountListEditor" );
                }
                if (0 != (variationFlags[0] & 0x02)) {
                    // 電子手順書バージョン（Easy Manual）
                    flags.Add( "/eManual" );
                }
                if (0 != (variationFlags[0] & 0x04)) {
                    // 廃止: DXFデータ対応
                    //flags.Add( "" );
                }
                if (0 != (variationFlags[0] & 0x08)) {
                    // 廃止: クリッピング機能
                    //flags.Add( "" );
                }
                if (0 != (variationFlags[0] & 0x10)) {
                    // CAFD Converter
                    flags.Add( "/Converter" );
                }
                if (0 != (variationFlags[0] & 0x20)) {
                    // イメージマーカー
                    //flags.Add( "" );
                }
                if (0 != (variationFlags[0] & 0x40)) {
                    // 目視検査バージョン（Visual Inspection）
                    //flags.Add( "/vInspect" );
                }
                if (0 != (variationFlags[0] & 0x80)) {
                    // 分割印刷
                    //flags.Add( "" );
                }

                if (0 != (variationFlags[1] & 0x01)) {
                    // オペレータ専用（電子手順書、配膳確認、等）
                    flags.Add( "/OperationOnly" );
                }
                if (0 != (variationFlags[1] & 0x02)) {
                    // ボードファイル読み取り専用
                    flags.Add( "/ReadOnly" );
                }
                if (0 != (variationFlags[1] & 0x04)) {
                    // 電子手順書ファイル高速検索
                    flags.Add( "/Everything" );
                }
                if (0 != (variationFlags[1] & 0x08)) {
                    // 
                    //flags.Add( "" );
                }
                if (0 != (variationFlags[1] & 0x10)) {
                    // ODB++Designデータ対応
                    flags.Add( "/ODB++" );
                }
                if (0 != (variationFlags[1] & 0x20)) {
                    // 配膳確認バージョン（Assembly Checker）
                    flags.Add( "/AseCker" );
                }

                return string.Join( ' ', flags );
            }
        }
        return "NG";
    };

    // プロセスハンドリング
    var cts = new CancellationTokenSource();
    var ct = cts.Token;
    Action Cancel = () => {
        //「CafdPP.exe」のプロセスは殺す
        CafdPP?.Kill();
        Console.WriteLine( " Process exited. Please restart `Launcher.exe`." );

        cts.Cancel();
    };

    // コンソールが閉じたとき
    Console.CancelKeyPress += ( sender, e ) => Cancel();

    // コンソール画面が閉じたとき
    //AppDomain.CurrentDomain.ProcessExit += ( sender, e ) => Cancel();
    _ = Task.Run( () => Program.Sub(
        ( i ) => {
            Cancel();
            return true;
        } )
    );

    // TODO:
    // `CafdPP.exe`アセンブリからGUIDを取得する（dll からしか取得できないかも？）
    // 
    // メッセージング
    _ = NlPipe.NamedPipeServer.CreatePipeServerAsync(
        "9B49E45A-E9C3-46F5-9CA2-E65BB26AE874", _MessageParser, ct );

    // コマンドライン引数の１つ目は、このプロセスを起動したファイルが格納される。
    var p = Environment.CommandLine;
    var i = p.IndexOf( '\"' );
    var j = p.IndexOf( '\"', i + 1 );
    if (i < j) {
        p = p.Remove( j ).Substring( i + 1 );
    }
    if (System.IO.File.Exists( p )) {

        // 起動している「CafdPP.exe」があれば監視する
        var exists = Process.GetProcessesByName( app );
        if (exists != null && 0 < exists.Length) {

            CafdPP = exists.First();
        }
        else {
            // 起動していなければ立ち上げる
            var startup = System.IO.Path.GetDirectoryName( p );
            if (startup != null) {
                do {
                    // 'Launcher.exe'と同じ場所を探す
                    var target = System.IO.Path.Combine( startup, exe );
                    if (System.IO.File.Exists( target )) {

                        CafdPP = Process.Start( target );
                        break;
                    }

                    // 既定の場所を探す
                    var def = System.IO.Path.Combine( @"C:\NewlyCoJp\CafdPP", exe );
                    if (System.IO.File.Exists( def )) {

                        CafdPP = Process.Start( def );
                        break;
                    }

                    // TODO: 圧縮ファイルの展開先になっている
                    //// JSONから最後の場所を探す
                    //var dat = Environment.GetEnvironmentVariable( "ProgramData" );
                    //if (dat != null) {
                    //    var json = System.IO.Path.Combine( dat, "NewlyCoJp", app, exe );
                    //    if (System.IO.File.Exists( json )) {
                    //
                    //        var buffer = System.IO.File.ReadAllText( json );
                    //        var x = buffer.IndexOf( "" );
                    //    }
                    //}

                } while (false);
            }
        }
    }

    while (!cts.IsCancellationRequested) {

        //「CafdPP.exe」のプロセスが閉じたら、新しいプロセスが立ち上がるのを監視する。
        if (CafdPP?.HasExited ?? true) {

            var exists = Process.GetProcessesByName( app );
            if (exists != null && 0 < exists.Length) {

                CafdPP = exists.First();
            }
            Thread.Sleep( 1000 );
        }
        else {

            Thread.Sleep( 3000 );
        }
    }
}
else {
    Console.WriteLine( "" );
    Console.WriteLine( "CAFD Plus+ AUTHENTICATION FAILURE. Please check USB dongle on your PC." );

}
Console.WriteLine( "Press any key to exit..." );
Console.ReadKey();

partial class Program
{
    [LibraryImport( "kernel32.dll", SetLastError = true )]
    private static partial IntPtr SetConsoleCtrlHandler( ConsoleCtrlDelegate Handler, int Add );

    // 終了シグナルをキャッチするデリゲート
    delegate bool ConsoleCtrlDelegate( int sig );

    static void Sub( Func<int, bool> handler )
    {
        // コンソールウィンドウが閉じられる場合の後処理
        SetConsoleCtrlHandler( new ConsoleCtrlDelegate( handler ), 1 );
        // コンソールが閉じられるまで待機
        Console.ReadLine();
    }




    [LibraryImport( "user32.dll", EntryPoint = "FindWindowW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16 )]
    private static partial IntPtr FindWindow( IntPtr lpClassName, string lpWindowName );

    [LibraryImport( "user32.dll", EntryPoint = "ShowWindow", SetLastError = true )]
    private static partial int ShowWindow( IntPtr hWnd, int nCmdShow );

    static void Min()
    {
        //var hWnd = FindWindow( "ConsoleWindowClass", IntPtr.Zero );
        var hWnd = FindWindow( IntPtr.Zero, Console.Title );
        if (hWnd != IntPtr.Zero) {

            ShowWindow( hWnd, 6 );
        }
    }
}
