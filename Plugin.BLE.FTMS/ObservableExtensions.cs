namespace Plugin.BLE.FTMS;

using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

public static class ObservableExtensions
{
	internal static IObservable<T> RetryAndDisconnect<T>(
			this ConnectionManager connectionManager,
			Func<ConnectionManager, IObservable<T>> sourceFactory,
			int maxExceptionCount = 10)
		=> maxExceptionCount > 0 ? 
			sourceFactory(connectionManager)
				.RetryWhen(exceptions => exceptions
				.SelectMany((exception, index) =>
				{
					connectionManager.LogExceptionDuringCreationOfFtmsConnection(
						exception,
						index,
						maxExceptionCount);
					return Observable
						.Timer(TimeSpan.FromSeconds(1) * index)
						.Select(_ => Unit.Default);
				})
				.Take(maxExceptionCount - 1)
				.Concat(exceptions
					.FirstAsync()
					.SelectMany(exception =>
					{
						connectionManager.LogExceptionDuringCreationOfFtmsConnection(
							exception,
							maxExceptionCount,
							maxExceptionCount);
						connectionManager.LogMaxExceptionCountReached(maxExceptionCount);
						return connectionManager.Disconnect().ToObservable();
					}))
				.Repeat()) :
			sourceFactory(connectionManager);
}
