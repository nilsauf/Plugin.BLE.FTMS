namespace Plugin.BLE.FTMS;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using ReactiveMarbles.ObservableEvents;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

public static class IAdapterExtensions
{
	public static IObservable<IDevice> ScanForDevices(
		this IAdapter adapter,
		ScanFilterOptions? scanFilterOptions = null,
		Func<IDevice, bool>? deviceFilter = null,
		bool allowDuplicatesKey = false)
	{
		ArgumentNullException.ThrowIfNull(adapter);
		return Observable.Create<IDevice>(async (obs, cancelToken) =>
		{
			var discoveredSub = adapter.Events()
				.DeviceDiscovered
				.Select(args => args.Device)
				.SubscribeSafe(obs);

			await adapter.StartScanningForDevicesAsync(scanFilterOptions, deviceFilter, allowDuplicatesKey, cancelToken);

			return new CompositeDisposable(
				discoveredSub,
				Disposable.Create(adapter, a => a.StopScanningForDevicesAsync()));
		});
	}
}
