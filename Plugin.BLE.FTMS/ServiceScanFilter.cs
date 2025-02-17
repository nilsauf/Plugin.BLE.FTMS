namespace Plugin.BLE.FTMS;

using global::FTMS.NET;
using Plugin.BLE.Abstractions;

public static class ServiceScanFilter
{
	public static ScanFilterOptions FitnessMachineService { get; }
		= new() { ServiceUuids = [FtmsUuids.Service] };
}
