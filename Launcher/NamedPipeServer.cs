using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NlPipe
{
    public class NamedPipeServer
    {
        public static Task CreatePipeServerAsync(string pipeName, Func<string, string> action, CancellationToken ct = default)
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 同じパイプに対しての接続は1件まで
                        ConsoleWriteLine("Server Start");
                        using (var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                        {
                            // クライアントの接続待ち
                            ConsoleWriteLine("Server Wait Connection");
                            await pipeServer.WaitForConnectionAsync(ct);

                            ConsoleWriteLine("Server StreamReader & StreamWriter");
                            using (var reader = new StreamReader(pipeServer))
                            using (var writer = new StreamWriter(pipeServer))
                            {
                                // 受信待ち
                                ConsoleWriteLine("Server <- Client Start");
                                var recvString = await reader.ReadLineAsync();
                                ConsoleWriteLine("Server <- Client End");
                                if (recvString != null)
                                {
                                    // アクション
                                    ConsoleWriteLine("Server Action Start");
                                    var result = action(recvString);
                                    ConsoleWriteLine("Server Action End");

                                    // 返信
                                    ConsoleWriteLine("Server -> Client Start");
                                    await writer.WriteLineAsync(result);
                                    writer.Flush();
                                    ConsoleWriteLine("Server -> Client End");
                                }
                            }
                        }
                    }
                    catch (IOException ofex)
                    {
                        // クライアントが切断
                        ConsoleWriteLine("Server Exception");
                        ConsoleWriteLine(ofex.Message);
                    }
                    catch (OperationCanceledException oce)
                    {
                        ConsoleWriteLine("Server Cancel");
                        ConsoleWriteLine(oce.Message);
                        // ループ抜ける
                        break;
                    }
                    finally
                    {
                        ConsoleWriteLine("Server Finish");
                    }
                }
            });
        }

        protected static void ConsoleWriteLine(string log)
        {
            //Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")} {log}");
        }
    }
}
