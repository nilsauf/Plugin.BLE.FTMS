namespace Plugin.BLE.FTMS;
using Plugin.BLE.Abstractions.Contracts;
using ReactiveMarbles.ObservableEvents;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

public static class ICharacteristicExtensions
{
	public static IObservable<byte[]> OnValueChanged(this ICharacteristic characteristic)
	{
		ArgumentNullException.ThrowIfNull(characteristic);
		return Observable.Create<byte[]>(async (obs, cancelToken) =>
		{
			var subscription = characteristic.Events()
				.ValueUpdated
				.Select(args => args.Characteristic.Value)
				.SubscribeSafe(obs);

			await characteristic.StartUpdatesAsync(cancelToken);

			return new CompositeDisposable(
				subscription,
				Disposable.Create(characteristic, c => c.StopUpdatesAsync()));
		});
	}
}
