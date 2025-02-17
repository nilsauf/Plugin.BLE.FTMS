namespace Plugin.BLE.FTMS;
using global::FTMS.NET;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Threading.Tasks;

internal sealed class PluginBleFtmsConnection(
	AdvertisementRecord serviceData,
	IService ftmsService)
	: IFitnessMachineServiceConnection
{
	public byte[] ServiceData { get; } = [.. serviceData.Data];

	public async Task<IFitnessMachineCharacteristic?> GetCharacteristicAsync(Guid id)
	{
		var characteristic = await ftmsService.GetCharacteristicAsync(id);

		return characteristic is not null ?
			new CharacteristicWrapper(characteristic) :
			null;
	}

	private sealed class CharacteristicWrapper(ICharacteristic characteristic)
		: IFitnessMachineCharacteristic
	{
		public Guid Id { get; } = characteristic.Id;

		public IObservable<byte[]> ObserveValue() => characteristic.OnValueChanged();

		public async Task<byte[]> ReadValueAsync()
		{
			var (data, _) = await characteristic.ReadAsync();
			return data;
		}

		public Task WriteValueAsync(byte[] value) => characteristic.WriteAsync(value);
	}
}
