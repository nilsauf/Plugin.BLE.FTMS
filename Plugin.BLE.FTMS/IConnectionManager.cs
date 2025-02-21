namespace Plugin.BLE.FTMS;

using DynamicData;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Threading.Tasks;

public interface IConnectionManager
{
	IObservableCache<IDevice, Guid> Devices { get; }
	IDevice? ConnectedDevice { get; }
	Task<bool> Connect(Guid deviceId);
	Task Disconnect();
	IObservable<bool> ObserveBluetoothAvailability();
	IObservable<IDevice?> ObserveConnectedDevice();
	void StartScanning(ScanFilterOptions? scanFilterOptions = null, Func<IDevice, bool>? deviceFilter = null);
	void StopScanning();
}