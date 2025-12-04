using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using static Nl.vtc.API;

// 役割を果たす
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

    static void RegistProcessExitedHandler( Process p, Action callback )
    {
        // プロセス終了時にイベントを要求する
        p.EnableRaisingEvents = true;

        void Process_Exited( object? sender, EventArgs e )
        {
            // このイベントは一度きり
            p.Exited -= Process_Exited;
            // コールバックを処理する
            callback();
        }
        p.Exited += Process_Exited;
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

    #region    Arguments()
    static string[] Arguments()=> Environment.CommandLine
            .Split( '/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );

    /// <summary>
    /// 監視のみを要求されたか
    /// </summary>
    static bool BeQuiet() => Arguments().Contains( "quiet", StringComparer.OrdinalIgnoreCase );
    #endregion(Arguments())


    /// <summary>
    /// 監視している「CAFD Plus+」のプロセス
    /// </summary>
    Process? CafdPP { get; set; } = null;

    /// <summary>
    /// リソースを解放してプログラムを終了させる
    /// </summary>
    #region    Dispose()
    void Dispose()
    {
        Console.WriteLine( " Process exited. Please restart `Launcher.exe`." );
        CafdPP?.Kill();
        cts.Cancel();

        this.IsDisposed = true;
    }
    CancellationTokenSource? _cts = null;
    CancellationTokenSource cts => _cts ??= new();
    CancellationToken ct => cts.Token;

    bool IsDisposed { get; set; } = false;
    #endregion(Dispose())

    /// <summary>
    /// 起動や終了、再起動イベントを待つ
    /// </summary>
    #region    WaitEvent()
    bool WaitEvent( ManualResetEventSlim @event )
    {
        try {

            @event.Wait( this.ct );
            return true;
        }
        catch (Exception) {

            //Console.WriteLine( "" );
            return false;
        }
        finally {

            @event.Reset();
        }
    }
    ManualResetEventSlim? @event = null;
    #endregion(WaitEvent())

    /// <summary>
    /// USBドングルのハンドルを更新する
    /// </summary>
    #region    PulseHandle()
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

                                                    if (0 != (variationFlags[1] & 0x01)) {
                                                        // 管理者用ドングルのハンドル返却を優先して、
                                                        // オペレータ専用だったときはループを続ける。
                                                        continue;
                                                    }
                                                    else {

                                                        return ErrCode.OK;
                                                    }
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
    readonly byte[] customerId = Encoding.ASCII.GetBytes( "Newly"/*.PadRight( 8, '\0' )*/  );
    byte[] versionASCII = null!;
    byte[] variationFlags = null!;
    #endregion(PulseHandle())

    /// <summary>
    /// プロセス間通信のメッセージをコマンドとして解釈する
    /// </summary>
    #region    ParseMessage()
    string ParseMessages( string message )
    {
        switch (message) {
            case "WhereAmI": {
                //「CAFD Plus+」が実行ファイルの場所を使用する
                return ExecLocation();
            }

            case "Authenticate": {

                //「CAFD Plus+」が生き返った場合に対応する
                if (this.CafdPP == null) {
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
                    flags.Add( "/AsmCker" );
                }

                return string.Join( ' ', flags );
            }
            case "Exit": {

                if (!BeQuiet()) {
                    // 自身も終了する
                    this.Dispose();
                }
                return "AllRight";
            }
        }
        return "MessageNotFound";
    }
    #endregion(ParseMessage())

    /// <summary>
    /// 指定の実行ファイルからプロセスを起動する
    /// </summary>
    #region    LaunchTarget
    void LaunchTarget( string app, bool launchingOnly = false )
    {
        var p = ExecLocation();
        var exe = $"{app}.exe";
        var json = $"{app}.json";

        if (launchingOnly) {
            // 起動していなければ起動して、かつ監視はしない。
            this.cts.Cancel();
        }

        do {
            if (this.CafdPP == null) {

                // 起動しているターゲットがあれば監視する
                var exists = Process.GetProcessesByName( app );
                // TODO: 多重起動していたらゾンビの可能性がある、殺してあげる？
                if (exists != null && 0 < exists.Length) {
                    if (@event == null) {
                        Console.WriteLine( " Attaching program..." );

                    }
                    this.CafdPP = exists.First();
                    continue;
                }
                else {
                    // 最初に起動させたプロセスが死んだら、ユーザーが起動しない限りは監視のみにしたい。
                    if (@event == null) {
                        if (BeQuiet()) {
                            Console.WriteLine( " Waiting for program start..." );

                            // 監視のみ
                            @event ??= new( false );
                            continue;
                        }
                        else {
                            Console.WriteLine( " Launching program..." );

                            // 1.ターゲットが起動していなければ立ち上げる
                            var startup = Path.GetDirectoryName( p );
                            if (startup != null) {
                                // このプロセスを起動したexeファイルと同じ場所を探す
                                var target = Path.Combine( startup, exe );
                                if (File.Exists( target )) {

                                    this.CafdPP = Process.Start( target );
                                    continue;
                                }
                            }

                            // 2.既定の場所を探す
                            var def = Path.Combine( @"C:\NewlyCoJp\CafdPP", exe );
                            if (File.Exists( def )) {

                                this.CafdPP = Process.Start( def );
                                continue;
                            }

                            // 3.環境ファイルから最後に起動した場所を探す
                            var dat = Environment.GetEnvironmentVariable( "ProgramData" );
                            if (dat != null) {
                                var cnf = Path.Combine( dat, "NewlyCoJp", app, json );
                                if (File.Exists( cnf )) {

                                    var buffer = File.ReadAllText( cnf );
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
                                            if (Directory.Exists( path )) {
                                                var latest = Path.Combine( path, exe );
                                                if (File.Exists( latest )) {

                                                    this.CafdPP = Process.Start( latest );
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else {

                        //「CAFD Plus+」が生まれる／生き返るまで眠る
                        _ = WaitEvent( @event );
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
                    //「CAFD Plus+」のプロセスが起動し（てい）た
                    Console.WriteLine( " Done." );

                    //「CAFD Plus+」に生命保険を掛ける
                    RegistProcessExitedHandler( this.CafdPP, () => @event.Set() );

                    //「CAFD Plus+」が死ぬまで眠る
                    if (!WaitEvent( @event )) {

                        Console.WriteLine( " Program has been exited." );
                    }
                }
            }
        } while (!this.cts.IsCancellationRequested);
    }
    #endregion(LaunchTarget)

    static void Proc()
    {
        const string Target = "CafdPP";

        // TODO: 環境ファイル「CafdPP.JSON」にアセンブリの場所があるので今ならGUIDは取得してこれるが...
        var guid = "9B49E45A-E9C3-46F5-9CA2-E65BB26AE874";
        using var mutex = new Mutex( true, $"{Target}:{{{guid}}}", out bool createdNew );
        if (createdNew) {

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

                // メッセージングを開始する
                _ = NlPipe.NamedPipeServer.CreatePipeServerAsync( guid, @this.ParseMessages, @this.ct );

                //「CAFD Plus+」を起動して監視する
                @this.LaunchTarget( Target );

            }
            else {
                Console.WriteLine( "" );
                Console.WriteLine( "CAFD Plus+ AUTHENTICATION FAILURE. Please check USB dongle on your PC." );

            }
            if (!@this.IsDisposed) {
                Console.WriteLine( "Press any key to exit..." );
                Console.ReadKey();

            }
        }
        else {
            Console.WriteLine( "Already running..." );

            // 自分はランチャーなので、ターゲットが死んでいるなら起動だけは試す。
            new Program().LaunchTarget( Target, true );
        }
    }
}
