using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MadWizard.WinUSBNet;

namespace Nl.vtc
{
	public class Manager
	{
        private static Manager? _Singleton = null;

		/// <summary>
		/// <see cref="Manager"/>の唯一のインスタンスを得る
		/// </summary>
		/// <returns></returns>
        public static Manager GetInstance() => _Singleton ??= new Manager();

		public void Refresh()
		{
			lock (_Lockey) {

				this.DeviceInfoList.Clear();

				try {
					USBDeviceInfo[] e = USBDevice.GetDevices( TargetDeviceInfo.DeviceInterfaceGUID );
					foreach (var i in e) {

						if (i.VID == TargetDeviceInfo.VendorID && i.PID == TargetDeviceInfo.ProductID) {
							this.DeviceInfoList.Add( i );
						}
					}
				}
				catch (Exception) {

					this.DeviceInfoList.Clear();
				}
			}
		}
		public void Clear()
		{
			lock (_Lockey) {

				this.DeviceInfoList.Clear();
			}
		}
		private readonly Lock _Lockey = new();

		public List<USBDeviceInfo> DeviceInfoList { get; set; } = new( 0x7f );



		//public void RegistNotifier( System.Windows.Forms.Control obj )
		//{
		//	USBNotifier DeviceNotifier = new USBNotifier( obj, TargetDeviceInfo.DeviceInterfaceGUID );
		//	if (DeviceNotifier != null) {
		//		DeviceNotifier.Arrival += new USBEventHandler( this.Arrival );
		//		DeviceNotifier.Removal += new USBEventHandler( this.Removal );
		//		this.DeviceNotifier = DeviceNotifier;
		//	}
		//}
		//public void Arrival( object sender, USBEvent e )
		//{
		//	USBDevice dev = new USBDevice( e.DevicePath );
		//	if (dev != null) {
		//		if (e.Guid.CompareTo( TargetDeviceInfo.DeviceInterfaceGUID ) == 0) {
		//			if (dev.Descriptor.VID == TargetDeviceInfo.VendorID
		//			&& dev.Descriptor.PID == TargetDeviceInfo.ProductID) {
		//
		//				if (this.Attached != null) {
		//					this.Attached( e );
		//				}
		//			}
		//		}
		//		dev.Dispose();
		//	}
		//}
		//public void Removal( object sender, USBEvent e )
		//{
		//	if (e.Guid.CompareTo( TargetDeviceInfo.DeviceInterfaceGUID ) == 0) {
		//
		//		if (this.Detached != null) {
		//			this.Detached( e );
		//		}
		//	}
		//}
		//public USBNotifier DeviceNotifier { get; private set; }
		//
		//
		//
		//// Formアプリケーション向け通知イベント
		//public event NotifyHandler Attached;
		//public event NotifyHandler Detached;
		//public delegate void NotifyHandler( USBEvent e );

	}
}
