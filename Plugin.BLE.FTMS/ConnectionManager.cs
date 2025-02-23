namespace Plugin.BLE.FTMS;
using DynamicData;
using global::FTMS.NET;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using ReactiveMarbles.ObservableEvents;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

public sealed partial class ConnectionManager : IDisposable, IConnectionManager
{
	private readonly SerialDisposable scanDisposable = new();
	private readonly CancellationDisposable cancellationDisposable = new();
	private readonly SourceCache<IDevice, Guid> devicesCache = new(d => d.Id);
	private readonly BehaviorSubject<IDevice?> connectedDevice = new(null);
	private readonly IObservable<IFitnessMachineServiceConnection?> currentServiceConnection;
	private readonly IBluetoothLE bluetoothLE;
	private readonly IAdapter adapter;
	private readonly ILogger<ConnectionManager> logger;
	private readonly IObservable<bool> bluetoothAvailability;

	public IDevice? ConnectedDevice => this.connectedDevice.Value;

	public IObservableCache<IDevice, Guid> Devices
		=> this.devicesCache.AsObservableCache();

	public ConnectionManager(
		IBluetoothLE bluetoothLE,
		IAdapter adapter,
		ILogger<ConnectionManager>? logger = null)
	{
		this.bluetoothLE = bluetoothLE;
		this.adapter = adapter;
		this.logger = logger ?? NullLogger<ConnectionManager>.Instance;

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
			.Catch((Exception ex) =>
			{
				this.LogExceptionDuringCreationOfFtmsConnection(ex);
				return Observable.Throw<IFitnessMachineServiceConnection>(ex);
			})
			.Retry(10)
			.Catch((Exception ex) => Observable.Return<IFitnessMachineServiceConnection>(null!))
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
		this.LogStartScanning();

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
		this.LogStopScanning();
		this.scanDisposable.Disposable = Disposable.Empty;
	}

	public async Task<bool> Connect(Guid deviceId)
	{
		if (this.devicesCache.KeyValues.TryGetValue(deviceId, out IDevice? device) == false)
		{
			this.LogDeviceNotFound(deviceId);
			return false;
		}

		int maxExceptionCount = 10;
		int exceptionCount = 0;
		return await Observable.Return(device)
			.SelectMany(async device =>
			{
				this.LogStartConnecting(device.Name);
				await this.adapter.ConnectToDeviceAsync(device);
				this.LogConnected(device.Name);
				return device;
			})
			.Catch((Exception ex) =>
			{
				exceptionCount++;
				this.LogExceptionDuringConnecting(device.Name, exceptionCount, maxExceptionCount, ex);
				return Observable.Throw<IDevice>(ex);
			})
			.Retry(maxExceptionCount)
			.Catch(Observable.Return<IDevice?>(null))
			.Do(this.connectedDevice.OnNext)
			.Select(connectedDevice => connectedDevice is not null)
			.FirstAsync();
	}

	public async Task Disconnect()
	{
		if (this.ConnectedDevice is null)
			return;

		this.LogStartDisconnecting(this.ConnectedDevice.Name);

		var deviceToDisconnect = this.ConnectedDevice;
		this.connectedDevice.OnNext(null);
		await this.adapter.DisconnectDeviceAsync(deviceToDisconnect);

		this.LogDisconnected(deviceToDisconnect.Name);
	}

	public void Dispose()
	{
		this.cancellationDisposable.Dispose();
		this.scanDisposable.Dispose();
		this.devicesCache.Dispose();
		this.connectedDevice.Dispose();
	}

	[LoggerMessage(LogLevel.Information, "Start Scanning for Devices")]
	private partial void LogStartScanning();

	[LoggerMessage(LogLevel.Information, "Stop Scanning for Devices")]
	private partial void LogStopScanning();

	[LoggerMessage(LogLevel.Warning, "Device with id {deviceId} was not found!")]
	private partial void LogDeviceNotFound(Guid deviceId);

	[LoggerMessage(LogLevel.Information, "Start Connecting to {deviceName}")]
	private partial void LogStartConnecting(string deviceName);

	[LoggerMessage(LogLevel.Information, "Connected to {deviceName}")]
	private partial void LogConnected(string deviceName);

	[LoggerMessage(LogLevel.Error, "Exception #{exceptionCount}/{maxExceptionCount} during connecting to {deviceName}")]
	private partial void LogExceptionDuringConnecting(string deviceName, int exceptionCount, int maxExceptionCount, Exception exception);

	[LoggerMessage(LogLevel.Information, "Start Disconnecting from {deviceName}")]
	private partial void LogStartDisconnecting(string deviceName);

	[LoggerMessage(LogLevel.Information, "Disconnected from {deviceName}")]
	private partial void LogDisconnected(string deviceName);

	[LoggerMessage(LogLevel.Error, "Exception during creation of FTMS Connection!")]
	private partial void LogExceptionDuringCreationOfFtmsConnection(Exception exception);
}
