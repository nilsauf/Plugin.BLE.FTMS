namespace Plugin.BLE.FTMS;
using global::FTMS.NET;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Linq;
using System.Reactive.Linq;

public static class IDeviceExtensions
{
	public static async Task<IFitnessMachineService> GetFitnessMachineServiceAsync(this IDevice device)
	{
		var connection = await device.CreateConnectionAsync();

		return await connection.CreateFitnessMachineServiceAsync();
	}

	public static async Task<IFitnessMachineServiceConnection> CreateConnectionAsync(this IDevice device)
	{
		ArgumentNullException.ThrowIfNull(device);

		var serviceData = device.AdvertisementRecords
			.FirstOrDefault(record => record.Type == AdvertisementRecordType.ServiceData)
			?? throw new InvalidOperationException();

		var ftms = await device.GetServiceAsync(FtmsUuids.Service) ?? throw new InvalidOperationException();

		return new PluginBleFtmsConnection(serviceData, ftms);
	}
}
