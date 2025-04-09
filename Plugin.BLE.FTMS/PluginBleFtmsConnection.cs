namespace Plugin.BLE.FTMS;
using global::FTMS.NET;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

internal sealed partial class PluginBleFtmsConnection(
	AdvertisementRecord serviceData,
	IService ftmsService,
	ILoggerFactory? loggerFactory = null)
	: IFitnessMachineServiceConnection
{
	private readonly ILogger logger = loggerFactory.ChreateLoggerSafely<PluginBleFtmsConnection>();

	public byte[] ServiceData { get; } = [.. serviceData.Data];

	public async Task<IFitnessMachineCharacteristic?> GetCharacteristicAsync(Guid id)
	{
		var characteristic = await ftmsService.GetCharacteristicAsync(id);

		if (characteristic is not null)
		{
			this.LogGotCharacteristic(characteristic.Name, characteristic.Id);

			var wrapperLogger = loggerFactory.ChreateLoggerSafely<CharacteristicWrapper>();
			return new CharacteristicWrapper(characteristic, wrapperLogger);
		}

		this.LogFailedToGetCharacteristic(FtmsUuids.GetName(id), id);
		return null;
	}

	[LoggerMessage(LogLevel.Trace, "Got characteristic '{characteristicName}'({characteristicId}) from FTMS service.")]
	private partial void LogGotCharacteristic(string characteristicName, Guid characteristicId);

	[LoggerMessage(LogLevel.Warning, "Failed to get characteristic '{characteristicName}'({characteristicId}) from FTMS service.")]
	private partial void LogFailedToGetCharacteristic(string characteristicName, Guid characteristicId);

	private sealed partial class CharacteristicWrapper(
			ICharacteristic characteristic,
			ILogger<CharacteristicWrapper> logger)
		: IFitnessMachineCharacteristic
	{
		public Guid Id { get; } = characteristic.Id;

		public string Name { get; } = characteristic.Name;

		public IObservable<byte[]> ObserveValue()
		{
			this.LogCharacteristicWillBeObserved(this.Name, this.Id);
			return characteristic.OnValueChanged()
				.Do(_ => this.LogCharacteristicWasNotified(this.Name, this.Id));
		}

		public async Task<byte[]> ReadValueAsync()
		{
			var (data, _) = await characteristic.ReadAsync();
			this.LogCharacteristicWasRead(this.Name, this.Id);
			return data;
		}

		public async Task WriteValueAsync(byte[] value)
		{
			var result = await characteristic.WriteAsync(value);
			this.LogCharacteristicWasWritten(this.Name, this.Id, result);
		}

		[LoggerMessage(LogLevel.Trace, "Characteristic '{characteristicName}'({characteristicId}) will be observed.")]
		private partial void LogCharacteristicWillBeObserved(string characteristicName, Guid characteristicId);

		[LoggerMessage(LogLevel.Trace, "Characteristic '{characteristicName}'({characteristicId}) was notified.")]
		private partial void LogCharacteristicWasNotified(string characteristicName, Guid characteristicId);

		[LoggerMessage(LogLevel.Trace, "Characteristic '{characteristicName}'({characteristicId}) was read.")]
		private partial void LogCharacteristicWasRead(string characteristicName, Guid characteristicId);

		[LoggerMessage(LogLevel.Trace, "Characteristic '{characteristicName}'({characteristicId}) was written. Result Value = {resultValue}")]
		private partial void LogCharacteristicWasWritten(string characteristicName, Guid characteristicId, int resultValue);
	}
}
