using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using MadWizard.WinUSBNet;

namespace Nl.vtc
{
	public partial class Device
	{
		internal class Driver : IDisposable
		{
			public Driver( USBDeviceInfo trg )
			{
				this.N_Lock = new USBDevice( trg );
			}
			~Driver()
			{
				this.Dispose();
			}
			public void Dispose()
			{
				this.N_Lock?.Dispose();
			}
			internal USBDevice N_Lock { get; }

			// USB I/F
			internal struct RequestType
			{
				public struct Direction
				{
					public const int HostToDevice = 0x00;
					public const int DeviceToHost = 0x80;
				};
				public struct Type
				{
					public const int Standard = 0x00;
					public const int Class = 0x20;
					public const int Vender = 0x40;
				};
				public struct Recip
				{
					public const int Device = 0x00;
					public const int Interface = 0x01;
					public const int Endpoint = 0x02;
					public const int Other = 0x03;
				};
			};

			// Vendor definition
			internal struct DeviceRequest
			{
				public struct Code
				{
					/// <summary>
					/// ワードアドレス設定
					/// </summary>
					public const int SetAddress = 0x02;
					/// <summary>
					/// ワードデータ読み込み
					/// </summary>
					public const int ReadData = 0x01;
					/// <summary>
					/// ワードデータ書き込み
					/// </summary>
					public const int WriteData = 0x03;
					/// <summary>
					/// バージョン取得
					/// </summary>
					public const int GetVersion = 0x00;
				};
				public struct Attr
				{
					/// <summary>
					/// 書き込み拒否
					/// </summary>
					public const byte Disabled = 0x00;
					/// <summary>
					/// 書き込み許可
					/// </summary>
					public const byte Enabled = 0x01;
				}
				public struct PacketSize
				{
					/// <summary>
					/// (単位サイズ)
					/// </summary>
					public const int Pack = 8;              //(転送データのパッケージサイズ)

					public const int SetAddress = Pack;     // アドレス部
					public const int ReadData = Pack * 2;   // ワード上位バイト部＋ワード下位バイト部
					public const int WriteData = Pack * 3;  // アドレス部＋ワード上位バイト部＋ワード下位バイト部
															//	public const int GetVersion = Pack;		// データ部
				};
				public struct PackBit
				{
					public const int SetAddress = 6;
					public const int ReadData = 7;
					public const int WriteData = 6;
				};
				public struct PackPattern
				{
					public const int SetAddress = 0;
					public const int HiByte = 1;
					public const int LwByte = 2;
				};
			};
			internal struct TransferConstant
			{
				public const int MaxRetryCount = 10;
				public const int RetryInterval = 20;
			};

			internal void UnpackByte( byte source, ref byte[] result, int bitIndex, int pattern )
			{
				int seed = DateTime.Now.Millisecond;

				switch (pattern) {
					case 0:
						result[0] = (byte)(seed * 3);
						result[1] = (byte)(seed * 5);
						result[2] = (byte)(seed * 1);
						result[3] = (byte)(seed * 7);
						result[4] = (byte)((seed * 5) ^ 0xff);
						result[5] = (byte)((seed * 1) ^ 0xff);
						result[6] = (byte)((seed * 7) ^ 0xff);
						result[7] = (byte)((seed * 3) ^ 0xff);
						break;
					case 1:
						result[0] = (byte)((seed * 7) ^ 0xff);
						result[1] = (byte)((seed * 3) ^ 0xff);
						result[2] = (byte)((seed * 5) ^ 0xff);
						result[3] = (byte)((seed * 1) ^ 0xff);
						result[4] = (byte)(seed * 1);
						result[5] = (byte)(seed * 7);
						result[6] = (byte)(seed * 5);
						result[7] = (byte)(seed * 3);
						break;
					default:
						result[0] = (byte)(seed * 1);
						result[1] = (byte)((seed * 5) ^ 0xff);
						result[2] = (byte)(seed * 3);
						result[3] = (byte)((seed * 7) ^ 0xff);
						result[4] = (byte)(seed * 7);
						result[5] = (byte)((seed * 1) ^ 0xff);
						result[6] = (byte)((seed * 3) ^ 0xff);
						result[7] = (byte)(seed * 5);
						break;
				}

				int filter = 1 << (bitIndex & 0x07);    // 8 bit ranged
				int mask = filter ^ 0xff;

				for (int i = 0; i < result.Length; i++) {
					if ((source & 0x80) != 0x00) {
						result[i] = (byte)(result[i] | filter);
					}
					else {
						result[i] = (byte)(result[i] & mask);
					}
					source = (byte)(source << 1);
				}
			}
			internal void PackByte( byte[] source, ref byte result, int bitIndex )
			{
				int filter = 1 << (bitIndex & 0x07);    // 8 bit ranged
				int mask = filter ^ 0xff;

				result = 0x00;

				for (int i = 0; i < source.Length; i++) {
					result = (byte)(result << 1);
					if ((source[i] & filter) != 0x00) {
						result = (byte)(result | 0x01);
					}
				}
			}
			/// <summary>
			/// N-Lockにワードアドレスを設定します
			/// </summary>
			/// <param name="addr">ワードアドレス</param>
			public bool SetAddress( byte addr )
			{
				byte[] packed = new byte[DeviceRequest.PacketSize.SetAddress];
				UnpackByte( addr, ref packed, DeviceRequest.PackBit.SetAddress, DeviceRequest.PackPattern.SetAddress );

				for (int retry = 0; retry < TransferConstant.MaxRetryCount; retry++) {

					try {
						this.N_Lock.ControlOut(
							RequestType.Direction.HostToDevice | RequestType.Type.Vender | RequestType.Recip.Device,
							DeviceRequest.Code.SetAddress,
							0x00,
							0x00,
							packed
						);
						// 正常終了
						return true;
					}
					catch (USBException ex) {
						// ドライバエラー
						Console.WriteLine( ex.Message );
						// インターバル後にリトライする
						System.Threading.Thread.Sleep( TransferConstant.RetryInterval );
					}
					catch (Exception ex) {
						// 意図しないエラー
						Console.WriteLine( ex.Message + Environment.NewLine + ex.StackTrace );
						// 致命的であるためリトライしない
						break;
					}
					finally {
						// F/W に内部処理を伴うコマンドは、成否に関わらずインターバルを設けます
						System.Threading.Thread.Sleep( 50 );
					}
				}
				return false;
			}
			/// <summary>
			/// N-Lockからワードデータを取得します
			/// </summary>
			/// <param name="hibyte">取得したワード上位データ</param>
			/// <param name="lwbyte">取得したワード下位データ</param>
			public bool ReadData( ref byte hibyte, ref byte lwbyte )
			{
				byte[] unpacked = new byte[DeviceRequest.PacketSize.ReadData];

				for (int retry = 0; retry < TransferConstant.MaxRetryCount; retry++) {

					try {
						this.N_Lock.ControlIn(
							RequestType.Direction.DeviceToHost | RequestType.Type.Vender | RequestType.Recip.Device,
							DeviceRequest.Code.ReadData,
							0x00,
							0x00,
							unpacked
						);

						PackByte( unpacked.Take( DeviceRequest.PacketSize.Pack ).ToArray(), ref hibyte, DeviceRequest.PackBit.ReadData );
						PackByte( unpacked.Skip( DeviceRequest.PacketSize.Pack ).ToArray(), ref lwbyte, DeviceRequest.PackBit.ReadData );

						// 正常終了
						return true;
					}
					catch (USBException ex) {
						// ドライバエラー
						Console.WriteLine( ex.Message );
						// インターバル後にリトライする
						System.Threading.Thread.Sleep( TransferConstant.RetryInterval );
					}
					catch (Exception ex) {
						// 意図しないエラー
						Console.WriteLine( ex.Message + Environment.NewLine + ex.StackTrace );
						// 致命的であるためリトライしない
						break;
					}
				}
				return false;
			}
		}
	}
}
