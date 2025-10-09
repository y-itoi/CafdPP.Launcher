using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Tasks;

namespace NlPipe
{
    public class NamedPipeClient
    {
        public static async Task CreateClientAsync( string pipeName, string writeString, Action<string> action )
        {
            await Task.Run( async () => {
                try {
                    ConsoleWriteLine( "Client Start" );
                    using (var pipeClient = new NamedPipeClientStream( ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation )) {
                        await pipeClient.ConnectAsync( 1000 );

                        ConsoleWriteLine( "Client StreamReader & StreamWriter" );
                        using (var reader = new StreamReader( pipeClient ))
                        using (var writer = new StreamWriter( pipeClient )) {
                            ConsoleWriteLine( "Client -> Server Start" );
                            await writer.WriteLineAsync( writeString );
                            writer.Flush();
                            ConsoleWriteLine( "Client -> Server End" );

                            // サーバーからの返信を受信
                            ConsoleWriteLine( "Client <- Server Start" );
                            string? response = await reader.ReadLineAsync();
                            ConsoleWriteLine( "Client <- Server End" );
                            if (response != null) {
                                ConsoleWriteLine( "Client Action Start" );
                                action( response );
                                ConsoleWriteLine( "Client Action End" );
                            }
                        }
                    }
                }
                catch (IOException ofex) {
                    ConsoleWriteLine( "Client Exception" );
                    ConsoleWriteLine( ofex.Message );
                }
                catch (TimeoutException timeout) {
                    ConsoleWriteLine( "Client Timeout" );
                    ConsoleWriteLine( timeout.Message );
                }
                finally {
                    ConsoleWriteLine( "Client Finish" );
                }
            } );
        }

        protected static void ConsoleWriteLine( string log )
        {
            //Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")} {log}");
        }
    }
}
