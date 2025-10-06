using System.Text;
using System.Runtime.InteropServices;
using static Nl.vtc.API;

Console.WriteLine("Hello, World!");


int c = 0;
int retcd = ErrCode.OK;

IntPtr handleOK = IntPtr.Zero;

retcd = ScnDev( ref c );
if (0 < c)
{
    Console.WriteLine($"ScnDev find {c} devices!!");

    var custumerId = Encoding.ASCII.GetBytes("Newly"/*.PadRight( 8, '\0' )*/ );

    for (int i = 0; i < c; i++)
    {
        var index = i + 1;

        retcd = CheckId(index, custumerId);
        if (retcd == ErrCode.OK)
        {

            var handle = IntPtr.Zero;
            retcd = GetHandle(index, ref handle);
            if (retcd == ErrCode.OK)
            {
                Console.WriteLine($"USB dongle found: {handle:X016}");





                handleOK = handle;
                break;
            }
            else
            {
                Console.WriteLine($"GetHandle failure by ({retcd})!!");

            }
        }
        else
        {
            Console.WriteLine($"CheckId failure by ({retcd})!!");

        }
    }
}
else
{
    Console.WriteLine("Divce not found!!");

}

string receivedMessage(string message)
{
    if (message == "Authenticate")
    {
        if (handleOK != IntPtr.Zero)
        {
            int retcd = Authenticate(handleOK);
            if (retcd == ErrCode.OK)
            {
                return "OK";
            }
        }
    }
    else if (message == "GetVersion")
    {
        
    }
    else if (message == "CheckFlag")
    {

    }
    return "NG";
}

var cancelServer = new CancellationTokenSource();
if (cancelServer != null)
{
    NlPipe.NamedPipeServer.CreatePipeServerAsync("sample", receivedMessage, cancelServer.Token);
}

Console.ReadKey();

if (cancelServer != null)
{
    cancelServer.Cancel();
}
