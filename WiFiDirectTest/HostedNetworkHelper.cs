using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.WiFiDirect;
using Windows.Devices.Enumeration;
using Windows.Networking;

namespace WiFiDirectTest
{
    public class HostedNetworkHelper
    {
        private IHostedNetworkListener _listener;
        private IHostedNetworkPrompt _prompt;

        /// tracks whether we should accept incoming connections or ask the user
        public bool AutoAccept { get; set; }

        private bool _ssidProvided;
        private string _ssid;
        public string SSID
        {
            get { return _ssid; }
            /// Set SSID (optional, falls back to a Wi-FI Direct default SSID which begins with "DIRECT-")
            set
            {
                _ssid = value;
                _ssidProvided = true;
            }
        }
        private bool _passPhraseProvided;
        private string _passPhrase;
        public string PassPhrase
        {
            get { return _passPhrase; }
            /// Set Passphrase (optional, falls back to a random string)
            set
            {
                _passPhrase = value;
                _passPhraseProvided = true;
            }
        }

        /// Main class that is used to start advertisement
        private WiFiDirectAdvertisementPublisher _publisher;
        /// Advertisement Settings within the publisher
        private WiFiDirectAdvertisement _advertisement;
        /// Legacy settings within the advertisement settings
        private WiFiDirectLegacySettings _legacySettings;
        /// Listen for incoming connections
        private WiFiDirectConnectionListener _connectionListener;
        /// Keep references to all connected peers. Key is DeviceId
        private Dictionary<string, WiFiDirectDevice> _connectedDevices;

        private object _threadObject = new Object();

        public HostedNetworkHelper()
        {
            _connectedDevices = new Dictionary<string, WiFiDirectDevice>();
            _ssidProvided = false;
            _passPhraseProvided = false;
            _listener = null;
            AutoAccept = true;
        }

        ~HostedNetworkHelper()
        {
            Reset();
        }

        /// Register listener to receive updates (only one listener is supported)
        public void RegisterListener(IHostedNetworkListener listener)
        {
            _listener = listener;
        }

        /// Register user prompt to get user input
        public void RegisterPrompt(IHostedNetworkPrompt prompt)
        {
            _prompt = prompt;
        }

        public void Start()
        {
            // Clean up old state
            Reset();

            // Create WiFiDirectAdvertisementPublisher
            _publisher = new WiFiDirectAdvertisementPublisher();
            // Add event handler for advertisement StatusChanged
            _publisher.StatusChanged += OnStatusChanged;

            // Set Advertisement required settings
            _advertisement = _publisher.Advertisement;

            // Must set the autonomous group owner (GO) enabled flag
            // Legacy Wi-Fi Direct advertisement uses a Wi-Fi Direct GO to act as an access point to legacy settings
            _advertisement.IsAutonomousGroupOwnerEnabled = true;

            _legacySettings = _advertisement.LegacySettings;

            // Must enable legacy settings so that non-Wi-Fi Direct peers can connect in legacy mode
            _legacySettings.IsEnabled = true;

            // Either specify an SSID, or read the randomly generated one
            if (_ssidProvided)
            {
                _legacySettings.Ssid = _ssid;
            }
            else
            {
                _ssid = _legacySettings.Ssid;
            }

            // Either specify a passphrase, or read the randomly generated one
            if (_passPhraseProvided)
            {
                _legacySettings.Passphrase.Password = _passPhrase;
            }
            else
            {
                _passPhrase = _legacySettings.Passphrase.Password;
            }

            _publisher.Start();
        }

        private void OnStatusChanged(WiFiDirectAdvertisementPublisher publisher, WiFiDirectAdvertisementPublisherStatusChangedEventArgs eventArgs)
        {
            try
            {
                switch (eventArgs.Status)
                {
                    case WiFiDirectAdvertisementPublisherStatus.Started:
                        {
                            // Begin listening for connections and notify listener that the advertisement started
                            StartListener();
                            if (_listener != null)
                            {
                                _listener.OnAdvertisementStarted();
                            }
                            break;
                        }
                    case WiFiDirectAdvertisementPublisherStatus.Stopped:
                        {
                            // Notify listener that the advertisement is stopped
                            if (_listener != null)
                            {
                                _listener.OnAdvertisementStopped("Advertisement stopped");
                            }
                            break;
                        }
                    case WiFiDirectAdvertisementPublisherStatus.Aborted:
                        {
                            // Check error and notify listener that the advertisement stopped
                            if (_listener != null)
                            {
                                string message;
                                switch (eventArgs.Error)
                                {
                                    case WiFiDirectError.RadioNotAvailable:
                                        message = "Advertisement aborted, Wi-Fi radio is turned off";
                                        break;
                                    case WiFiDirectError.ResourceInUse:
                                        message = "Advertisement aborted, Resource In Use";
                                        break;
                                    default:
                                        message = "Advertisement aborted, unknown reason";
                                        break;
                                }
                                _listener.OnAdvertisementAborted(message);
                            }
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                if (_listener != null)
                {
                    _listener.OnAsyncException(ex.ToString());
                }
                return;
            }
        }

        public void Stop()
        {
            // Call stop on the publisher and expect the status changed callback
            if (_publisher != null
                && (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Created
                    || _publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started))
            {
                _publisher.Stop();
            }
            else
            {
                _listener.OnAdvertisementStopped("Advertisement status is null or bad status");
            }
        }

        private void StartListener()
        {
            // Create WiFiDirectConnectionListener
            _connectionListener = new WiFiDirectConnectionListener();
            _connectionListener.ConnectionRequested += OnConnectionRequested;
            if (_listener != null)
            {
                _listener.LogMessage("Connection Listener is ready");
            }
        }

        private async void OnConnectionRequested(WiFiDirectConnectionListener listener, WiFiDirectConnectionRequestedEventArgs eventArgs)
        {
            if (_listener != null)
            {
                _listener.LogMessage("Connection Requested...");
            }
            bool acceptConnection = true;
            if (!AutoAccept && _prompt != null)
            {
                acceptConnection = _prompt.AcceptIncommingConnection();
            }

            try
            {
                WiFiDirectConnectionRequest request = eventArgs.GetConnectionRequest();
                if (acceptConnection)
                {
                    DeviceInformation deviceInformation = request.DeviceInformation;
                    string deviceId = deviceInformation.Id;

                    // Must call FromIdAsync first   
                    var tcsWiFiDirectDevice = new TaskCompletionSource<WiFiDirectDevice>();
                    var wfdDeviceTask = tcsWiFiDirectDevice.Task;
                    tcsWiFiDirectDevice.SetResult(await WiFiDirectDevice.FromIdAsync(deviceId));
                    // Get the WiFiDirectDevice object
                    WiFiDirectDevice wfdDevice = await wfdDeviceTask;

                    // Now retrieve the endpoint pairs, which includes the IP address assigned to the peer
                    var endpointPairs = wfdDevice.GetConnectionEndpointPairs();
                    string remoteHostName = "";
                    if (endpointPairs.Any())
                    {
                        EndpointPair endpoint = endpointPairs[0];
                        remoteHostName = endpoint.RemoteHostName.DisplayName;
                    }
                    else
                    {
                        throw new Exception("Can't retrieve endpoint pairs");
                    }

                    // Add handler for connection status changed
                    wfdDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                    // Store the connected peer 
                    lock (_threadObject)
                    {
                        _connectedDevices.Add(wfdDevice.DeviceId, wfdDevice);
                    }

                    // Notify Listener
                    if (_listener != null)
                    {
                        _listener.OnDeviceConnected(remoteHostName);
                    }
                }
                else
                {
                    if (_listener != null)
                    {
                        _listener.LogMessage("Declined");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_listener != null)
                {
                    _listener.OnAsyncException(ex.ToString());
                }
                return;
            }
        }

        private void OnConnectionStatusChanged(WiFiDirectDevice wfdDeviceSender, object obj)
        {
            try
            {
                switch (wfdDeviceSender.ConnectionStatus)
                {
                    case WiFiDirectConnectionStatus.Disconnected:
                        {
                            string deviceId = wfdDeviceSender.DeviceId;
                            if (_connectedDevices.ContainsKey(deviceId))
                            {
                                _connectedDevices[deviceId].ConnectionStatusChanged -= OnConnectionStatusChanged;
                                _connectedDevices.Remove(deviceId);
                            }
                            // Notify listener of disconnect
                            if (_listener != null)
                            {
                                _listener.OnDeviceDisconnected(deviceId);
                            }
                            break;
                        }
                    case WiFiDirectConnectionStatus.Connected:
                        //ignored
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_listener != null)
                {
                    _listener.OnAsyncException(ex.ToString());
                }
                return;
            }
        }

        private void Reset()
        {
            if (_connectionListener != null)
            {
                _connectionListener.ConnectionRequested -= OnConnectionRequested;
            }
            if (_publisher != null)
            {
                _publisher.StatusChanged -= OnStatusChanged;
                if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Created
                    || _publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
                    _publisher.Stop();
            }
            _connectionListener = null;
            _publisher = null;
            _legacySettings = null;
            _advertisement = null;
            _connectedDevices.Clear();
        }

        public override string ToString()
        {
            string sepBig = "/-----------------------------------------------------------------------------------/";
            string sepSmall = "*************************************************************************************";

            StringBuilder resStr = new StringBuilder();

            resStr.AppendLine(sepBig);
            resStr.AppendLine("Publisher information:");
            if (_publisher != null)
            {
                resStr.AppendFormat("Status............................. : {0}", _publisher.Status).AppendLine();

                resStr.AppendLine(sepSmall);

                resStr.AppendLine("Advertisement information:");
                if (_advertisement != null)
                {
                    resStr.AppendFormat("IsAutonomousGroupOwnerEnabled...... : {0}", _advertisement.IsAutonomousGroupOwnerEnabled).AppendLine();
                    resStr.AppendFormat("ListenStateDiscoverability......... : {0}", _advertisement.ListenStateDiscoverability).AppendLine();
                }
                else
                {
                    resStr.AppendLine("Advertisement is not created");
                }

                resStr.AppendLine(sepSmall);

                resStr.AppendLine("Advertisement's Legacy Settings information:");
                if (_advertisement != null)
                {
                    resStr.AppendFormat("IsEnabled.......................... : {0}", _legacySettings.IsEnabled).AppendLine();
                    resStr.AppendFormat("Ssid............................... : {0}", _legacySettings.Ssid).AppendLine();
                    resStr.AppendFormat("Passphrase......................... : {0}", _legacySettings.Passphrase.Password).AppendLine();
                }
                else
                {
                    resStr.AppendLine("Advertisement's Legacy Settings is not set");
                }
                
                resStr.AppendLine(sepSmall);

                resStr.AppendLine("Connected devices information:");
                if (_connectedDevices.Any())
                {
                    foreach (var connDevKey in _connectedDevices.Keys)
                    {
                        resStr.AppendFormat("DeviceId.......................... : {0}", connDevKey).AppendLine();
                        var wfdDevice = _connectedDevices[connDevKey];
                        resStr.AppendFormat("ConnectionStatus.................. : {0}", wfdDevice.ConnectionStatus).AppendLine();
                        var endpointPairs = wfdDevice.GetConnectionEndpointPairs();
                        for (int i = 0; i < endpointPairs.Count; i++)
                        {
                            resStr.AppendLine();
                            resStr.AppendFormat("Endpoint pair information No...... : {0}", i).AppendLine();
                            resStr.AppendFormat("Local Host Name................... : {0}", endpointPairs[i].LocalHostName.DisplayName).AppendLine();
                            resStr.AppendFormat("Local Service Name................ : {0}", endpointPairs[i].LocalServiceName).AppendLine();
                            resStr.AppendFormat("Remote Host Name.................. : {0}", endpointPairs[i].RemoteHostName.DisplayName).AppendLine();
                            resStr.AppendFormat("Remote Service Name............... : {0}", endpointPairs[i].RemoteServiceName).AppendLine();
                            resStr.AppendLine();
                        }
                    }
                }
                else
                {
                    resStr.AppendLine("No connected devices found");
                }

                resStr.AppendLine(sepSmall);
            }
            else
            {
                resStr.AppendLine("Publisher is not created!");
            }
            resStr.AppendLine(sepBig);

            return resStr.ToString();
        }
    }
}
