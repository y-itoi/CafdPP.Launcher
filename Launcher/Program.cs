using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using static Nl.vtc.API;

// 
Program.Proc();

partial class Program
{
    #region    RegistExitedHandler()
    delegate bool ConsoleCtrlDelegate( int sig );

    [LibraryImport( "kernel32.dll", SetLastError = true )]
    private static partial IntPtr SetConsoleCtrlHandler( ConsoleCtrlDelegate Handler, int Add );

    static void RegistExitedHandler( Action callback )
    {
        Func<int, bool> handler = sig => {
            // コールバックを処理する
            callback();
            // このイベントは一度きり
            return true;
        };

        // コンソールが閉じられるときの処理を構成する
        SetConsoleCtrlHandler( new ConsoleCtrlDelegate( handler ), 1 );
        // コンソールが閉じられるまで待機
        _ = Task.Run( Console.ReadLine );
    }
    #endregion(RegistExitedHandler())

    #region    MinimiseSelf())
    [LibraryImport( "user32.dll", EntryPoint = "FindWindowW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16 )]
    private static partial IntPtr FindWindow( IntPtr lpClassName, string lpWindowName );

    [LibraryImport( "user32.dll", SetLastError = true )]
    private static partial int ShowWindow( IntPtr hWnd, int nCmdShow );

    static void MinimiseSelf()
    {
        var hWnd = FindWindow( IntPtr.Zero, Console.Title );
        if (hWnd != IntPtr.Zero) {

            ShowWindow( hWnd, 6 );
        }
    }
    #endregion(MinimiseSelf()

    #region    ExecLocation()
    static string ExecLocation()
    {
        var p = Environment.CommandLine;
        if (p.StartsWith( '\"' )) {
            var i = p.IndexOf( '\"' );
            var j = p.IndexOf( '\"', i + 1 );
            if (i < j) {
                // ダブルクォーテーションを除いて返す
                return p[..j][(i + 1)..];
            }
            else {
                // 先頭にだけダブルクォーテーションが存在するパターンはないはずだが...
                return p[(i + 1)..];
            }
        }
        else {
            // コマンドライン引数の最初のパラメータを返す
            return p.Split( ' ' ).First();
        }
    }
    #endregion(ExecLocation())

    CancellationTokenSource? _cts = null;
    CancellationTokenSource cts => _cts ??= new();
    CancellationToken ct => cts.Token;

    /// <summary>
    /// 監視している「CAFD Plus+」のプロセス
    /// </summary>
    Process? CafdPP { get; set; } = null;

    void Dispose()
    {
        Console.WriteLine( " Process exited. Please restart `Launcher.exe`." );
        CafdPP?.Kill();
        cts.Cancel();
    }

    readonly byte[] customerId = Encoding.ASCII.GetBytes( "Newly"/*.PadRight( 8, '\0' )*/  );
    byte[] versionASCII = null!;
    byte[] variationFlags = null!;

    ManualResetEventSlim? @event = null;

    int PulseHandle( out IntPtr handleCorrect )
    {
        handleCorrect = IntPtr.Zero;

        int retcd = ScnDev( out int c );
        if (retcd == ErrCode.OK) {
            //Console.WriteLine( $"ScnDev find {c} devices!!" );

            for (int i = 0, index = i + 1; i < c; i++) {

                retcd = CheckId( index, customerId );
                if (retcd == ErrCode.OK) {

                    retcd = GetHandle( index, out IntPtr handle );
                    if (retcd == ErrCode.OK) {
                        //Console.WriteLine( $"USB dongle found: {handle:X016}" );

                        // 認証
                        retcd = GetMem( index, 0x00, 8, out byte[] regist );
                        if (retcd == ErrCode.OK) {

                            // ｢CAFD Plus｣ & ｢CAFD Plus+｣フラグ確認
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
                                        // ver[4]: 'D' Major Version
                                        // ver[5]: '.'
                                        // ver[6]: 'D'
                                        // ver[7]: 'D' Minor Version
                                        var majorNumber = Encoding.ASCII.GetString( versionASCII, 4, 1 );
                                        if (int.TryParse( majorNumber, out int major )) {

                                            var minorNumber = Encoding.ASCII.GetString( versionASCII, 6, 2 );
                                            if (int.TryParse( minorNumber, out int minor )) {

                                                // ここで期待されるのは「Ver.3.00.0000」の登録があるドングル
                                                if (major * 100 + minor < 300) { }
                                                else {

                                                    handleCorrect = handle;
                                                    return ErrCode.OK;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            retcd = ErrCode.NotFound;
                            //Console.WriteLine( $"CAFD Plus+ didn't REGISTERD!!" );

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

    string ParseMessages( string message )
    {
        switch (message) {
            case "Authenticate": {

                //「CAFD Plus+」が生き返った場合に対応する
                if (CafdPP == null) {
                    // 監視していないプロセスからメッセージがきた、つまり生き返った。
                    if (@event != null) {
                        @event.Set();
                    }
                }

                int retcd = PulseHandle( out IntPtr hCurrent );
                if (retcd == ErrCode.OK) {

                    retcd = Authenticate( hCurrent );
                    if (retcd == ErrCode.OK) {

                        return "OK";
                    }
                }
                return "Failed";
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
        return "MessageNotFound";
    }

    void LaunchTarget( string app )
    {
        var p = ExecLocation();
        var exe = $"{app}.exe";
        var json = $"{app}.json";

        Console.WriteLine( " Launching program..." );
        do {
            if (this.CafdPP == null) {

                // TODO: 多重起動していたらゾンビの可能性がある、殺してあげる？
                // 
                // 起動しているターゲットがあれば監視する
                var exists = Process.GetProcessesByName( app );
                if (exists != null && 0 < exists.Length) {

                    this.CafdPP = exists.First();
                    continue;
                }
                else {
                    // 最初に起動させたプロセスが死んだら、ユーザーが起動しない限りは監視のみにしたい。
                    if (@event == null) {

                        // ターゲットが起動していなければ立ち上げる
                        var startup = System.IO.Path.GetDirectoryName( p );
                        if (startup != null) {
                            // このプロセスを起動したexeファイルと同じ場所を探す
                            var target = System.IO.Path.Combine( startup, exe );
                            if (System.IO.File.Exists( target )) {

                                this.CafdPP = Process.Start( target );
                                continue;
                            }
                        }

                        // 既定の場所を探す
                        var def = System.IO.Path.Combine( @"C:\NewlyCoJp\CafdPP", exe );
                        if (System.IO.File.Exists( def )) {

                            this.CafdPP = Process.Start( def );
                            continue;
                        }

                        // TODO: 環境ファイル「CafdPP.JSON」にあるアセンブリの場所が圧縮ファイルの展開先になっている...
                        // TODO: このロジックは外部からインジェクションするべきかも
                        //
                        // 環境ファイルから最後に起動した場所を探す
                        var dat = Environment.GetEnvironmentVariable( "ProgramData" );
                        if (dat != null) {
                            var cnf = System.IO.Path.Combine( dat, "NewlyCoJp", app, json );
                            if (System.IO.File.Exists( cnf )) {

                                var buffer = System.IO.File.ReadAllText( cnf );
                                using var doc = JsonDocument.Parse( buffer );

                                JsonElement? @class = null;
                                foreach (var node in doc.RootElement.EnumerateObject()) {
                                    if (node.Name.Equals( "Desktop.Config.Base", StringComparison.OrdinalIgnoreCase )) {

                                        @class = node.Value;
                                        break;
                                    }
                                }
                                if (@class != null) {

                                    JsonElement? prop = null;
                                    foreach (var node in @class.Value.EnumerateObject()) {
                                        if (node.Name.Equals( "Location", StringComparison.OrdinalIgnoreCase )) {

                                            prop = node.Value;
                                            break;
                                        }
                                    }
                                    if (prop != null) {

                                        var path = prop.Value.GetString() ?? "";
                                        if (System.IO.Directory.Exists( path )) {
                                            var latest = System.IO.Path.Combine( path, exe );
                                            if (System.IO.File.Exists( latest )) {

                                                this.CafdPP = Process.Start( latest );
                                                continue;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                    else {

                        try {
                            //「CAFD Plus+」が生き返るまで眠る
                            @event.Wait( this.ct );
                        }
                        catch (Exception) {

                            //Console.WriteLine( " Program has restarted." );
                        }
                        finally {

                            @event.Reset();
                        }
                        continue;
                    }
                }
                //「CAFD Plus+」のプロセスを開始できなかった
                Console.WriteLine( " Program not found !!" );
                break;
            }
            else {
                //「CAFD Plus+」のプロセスが閉じたら、新しいプロセスが立ち上がるのを監視する。
                if (this.CafdPP?.HasExited ?? true) {
                    this.CafdPP = null;

                    Console.WriteLine( " Waiting for program restart..." );
                }
                else {
                    if (@event == null) {
                        @event = new( false );

                        // これ以降、画面は表示しなくてよい。
                        MinimiseSelf();
                    }
                    Console.WriteLine( " Done." );

                    //「CAFD Plus+」に生命保険を掛ける
                    this.CafdPP.EnableRaisingEvents = true;
                    this.CafdPP.Exited += ( sender, e ) => @event.Set();

                    try {
                        //「CAFD Plus+」が死ぬまで眠る
                        @event.Wait( this.ct );
                    }
                    catch (Exception) {

                        //Console.WriteLine( " Program has been exited." );
                    }
                    finally {

                        @event.Reset();
                    }
                }
            }
        } while (!this.cts.IsCancellationRequested);
    }

    static void Proc()
    {
        Console.WriteLine( "_/ (C) 2025 NEWLY CORPORATION" );
        Console.WriteLine( "_/ CAFD Plus+" );
        Console.WriteLine( "_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/" );
        Console.WriteLine( "" );
        var @this = new Program();

        int retcd = @this.PulseHandle( out IntPtr handle );
        if (retcd == ErrCode.OK) {

            // コンソールが閉じたとき「CAFD Plus+」を殺す
            Console.CancelKeyPress += ( sender, e ) => @this.Dispose();

            // コンソールでプログラムが終了したとき「CAFD Plus+」を殺す
            RegistExitedHandler( @this.Dispose );

            // TODO: 環境ファイル「CafdPP.JSON」にアセンブリの場所があるのでGUIDは取得してこれるはず
            //
            // メッセージングを開始する
            _ = NlPipe.NamedPipeServer.CreatePipeServerAsync(
                "9B49E45A-E9C3-46F5-9CA2-E65BB26AE874", @this.ParseMessages, @this.ct );

            //「CAFD Plus+」を起動して監視する
            @this.LaunchTarget( "CafdPP" );

        }
        else {
            Console.WriteLine( "" );
            Console.WriteLine( "CAFD Plus+ AUTHENTICATION FAILURE. Please check USB dongle on your PC." );

        }
        Console.WriteLine( "Press any key to exit..." );
        Console.ReadKey();
    }
}
