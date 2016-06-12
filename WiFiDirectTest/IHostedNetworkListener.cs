
namespace WiFiDirectTest
{
    public interface IHostedNetworkListener
    {
        void OnDeviceConnected(string remoteHostIP);
        void OnDeviceDisconnected(string deviceId);

        void OnAdvertisementStarted();
        void OnAdvertisementStopped(string message);
        void OnAdvertisementAborted(string message);

        void OnAsyncException(string message);

        void LogMessage(string message);
    }
}
