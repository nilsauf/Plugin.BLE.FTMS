namespace Plugin.BLE.FTMS;
using DynamicData;
using global::FTMS.NET;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using ReactiveMarbles.ObservableEvents;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

public sealed class ConnectionManager : IDisposable, IConnectionManager
{
	private readonly SerialDisposable scanDisposable = new();
	private readonly CancellationDisposable cancellationDisposable = new();
	private readonly SourceCache<IDevice, Guid> devicesCache = new(d => d.Id);
	private readonly BehaviorSubject<IDevice?> connectedDevice = new(null);
	private readonly IObservable<IFitnessMachineServiceConnection?> currentServiceConnection;
	private readonly IBluetoothLE bluetoothLE;
	private readonly IAdapter adapter;
	private readonly IObservable<bool> bluetoothAvailability;

	public IDevice? ConnectedDevice => this.connectedDevice.Value;

	public IObservableCache<IDevice, Guid> Devices
		=> this.devicesCache.AsObservableCache();

	public ConnectionManager(IBluetoothLE bluetoothLE, IAdapter adapter)
	{
		this.bluetoothLE = bluetoothLE;
		this.adapter = adapter;

		this.bluetoothAvailability = this.bluetoothLE.Events()
			.StateChanged
			.TakeUntil(this.cancellationDisposable.Token)
			.Select(args => args.NewState)
			.StartWith(this.bluetoothLE.State)
			.Select(state => state == BluetoothState.On)
			.Replay(1)
			.RefCount();

		this.currentServiceConnection = this.connectedDevice
			.SelectMany(connectedDevice => connectedDevice is null ?
				Task.FromResult<IFitnessMachineServiceConnection>(null!) :
				connectedDevice.CreateConnectionAsync())
			.Replay(1)
			.AutoConnect();
	}

	public IObservable<IDevice?> ObserveConnectedDevice()
		=> this.connectedDevice.AsObservable();

	public IObservable<bool> ObserveBluetoothAvailability()
		=> this.bluetoothAvailability.AsObservable();

	public IObservable<IFitnessMachineServiceConnection?> ObserveCurrentServiceConnection()
		=> this.currentServiceConnection.AsObservable();

	public void StartScanning(ScanFilterOptions? scanFilterOptions = null, Func<IDevice, bool>? deviceFilter = null)
	{
		this.devicesCache.Clear();
		this.scanDisposable.Disposable = this.bluetoothAvailability
			.Select(availbility => availbility switch
			{
				false => ObserveIfNotAvailable(),
				true => ObserveIfAvailable()
			})
			.Switch()
			.ToObservableChangeSet(d => d.Id)
			.PopulateInto(this.devicesCache);

		IObservable<IDevice> ObserveIfNotAvailable() => Observable.Defer(
			() =>
			{
				this.devicesCache.Clear();
				return Observable.Never<IDevice>();
			});

		IObservable<IDevice> ObserveIfAvailable() => Observable.Defer(
			() => this.adapter.ScanForDevices(scanFilterOptions, deviceFilter));
	}

	public void StopScanning()
	{
		this.scanDisposable.Disposable = Disposable.Empty;
	}

	public async Task<bool> Connect(Guid deviceId)
	{
		if (this.devicesCache.KeyValues.TryGetValue(deviceId, out IDevice? device) == false)
		{
			Debug.WriteLine($"{DateTime.Now} - Device was not found!");
			return false;
		}

		return await Observable.Return(device)
			.SelectMany(async device =>
			{
				Debug.WriteLine($"{DateTime.Now} - Start Connecting to {device.Name}");
				await this.adapter.ConnectToDeviceAsync(device);
				Debug.WriteLine($"{DateTime.Now} - Connected to {device.Name}!");
				return device;
			})
			.Catch((Exception ex) =>
			{
				Debug.WriteLine($"{DateTime.Now} - Exception: {ex.GetType().Name} - Message: {ex.Message}");
				return Observable.Throw<IDevice>(ex);
			})
			.Retry(10)
			.Do(this.connectedDevice.OnNext)
			.Select(connectedDevice => connectedDevice is not null)
			.FirstAsync();
	}

	public async Task Disconnect()
	{
		if (this.ConnectedDevice is null)
			return;

		Debug.WriteLine($"{DateTime.Now} - Start Disconnecting from {this.ConnectedDevice.Name}");

		var deviceToDisconnect = this.ConnectedDevice;
		this.connectedDevice.OnNext(null);
		await this.adapter.DisconnectDeviceAsync(deviceToDisconnect);

		Debug.WriteLine($"{DateTime.Now} - Disconnected!");
	}

	public void Dispose()
	{
		this.cancellationDisposable.Dispose();
		this.scanDisposable.Dispose();
		this.devicesCache.Dispose();
		this.connectedDevice.Dispose();
	}
}
