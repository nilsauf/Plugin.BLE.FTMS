namespace Plugin.BLE.FTMS;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ILoggerFactoryExtensions
{
	public static ILogger<T> ChreateLoggerSafely<T>(this ILoggerFactory? loggerFactory)
		where T : class
	{
		return loggerFactory?.CreateLogger<T>()
			?? NullLogger<T>.Instance;
	}
}
