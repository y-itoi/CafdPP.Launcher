using System.Text;
using static Nl.vtc.API;

Console.WriteLine("Hello, World!");


int c = 0;
int retcd = ErrCode.OK;

retcd = ScnDev( ref c );
if (0 < c) {
    Console.WriteLine( $"ScnDev find {c} devices!!" );

    var custumerId = Encoding.ASCII.GetBytes( "Newly"/*.PadRight( 8, '\0' )*/ );

    for (int i = 0; i < c; i++) {
        var index = i + 1;

        retcd = CheckId( index, custumerId );
        if (retcd == ErrCode.OK) {

            var handle = IntPtr.Zero;
            retcd = GetHandle( index, ref handle );
            if (retcd == ErrCode.OK) {
                Console.WriteLine( $"USB dongle found: {handle:X016}" );






            }
            else {
                Console.WriteLine( $"GetHandle failure by ({retcd})!!" );

            }
        }
        else {
            Console.WriteLine( $"CheckId failure by ({retcd})!!" );

        }
    }
}
else {
    Console.WriteLine( "Divce not found!!" );

}
Console.ReadKey();
