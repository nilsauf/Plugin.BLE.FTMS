namespace Plugin.BLE.FTMS;

using Plugin.BLE.Abstractions;

public static class ServiceScanFilter
{
	public static ScanFilterOptions FitnessMachineService { get; }
		= new() { ServiceUuids = [FtmsUuids.Service] };
}
