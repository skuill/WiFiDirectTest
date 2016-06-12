using System;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;

namespace WiFiDirectTest
{
    public class SimpleConsole : IHostedNetworkListener, IHostedNetworkPrompt
    {
        private HostedNetworkHelper _hostedNetwork;
        ManualResetEvent _evtObj;

        // Singleton
        private static SimpleConsole _instance;
        // Singleton access
        public static SimpleConsole Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SimpleConsole();
                }
                return _instance;
            }
        }

        public SimpleConsole()
        {
            _evtObj = new ManualResetEvent(false);
            _hostedNetwork = new HostedNetworkHelper();
            _hostedNetwork.RegisterListener(this);
            _hostedNetwork.RegisterPrompt(this);
        }

        public void RunConsole()
        {
            string command;
            bool isRunning = true;
            while (isRunning)
            {
                // Show prompt and get input
                ShowPrompt();
                command = Console.ReadLine();
                // Run the command, return false if the command was to quit
                try
                {
                    isRunning = ExecuteCommand(command);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(String.Format("Caught Exception: {0}", ex.Message));
                }
            }
        }

        public void OnDeviceConnected(string remoteHostName)
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine()
                .Append("Peer connected: ")
                .AppendLine(remoteHostName);
            Console.Write(str);
        }

        public void OnDeviceDisconnected(string deviceId)
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine()
                .Append("Peer disconnected: ")
                .AppendLine(deviceId);
            Console.Write(str);
        }

        public void OnAsyncException(string message)
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine()
                .Append("Caught exception in asynchronous method: ")
                .AppendLine(message);
            Console.Write(str);
        }

        public void OnAdvertisementStarted()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine("Soft AP started!")
                .Append("Peers can connect to: ")
                .AppendLine(_hostedNetwork.SSID)
                .Append("Passphrase: ")
                .AppendLine(_hostedNetwork.PassPhrase);
            Console.Write(str);
            _evtObj.Set();
        }

        public void OnAdvertisementStopped(string message)
        {
            StringBuilder str = new StringBuilder();
            str.Append("Soft AP stopped: ")
                .AppendLine(message);
            Console.Write(str);
            _evtObj.Set();
        }

        public void OnAdvertisementAborted(string message)
        {
            StringBuilder str = new StringBuilder();
            str.Append("Soft AP aborted: ")
                .AppendLine(message);
            Console.Write(str);
            _evtObj.Set();
        }

        public void LogMessage(string message)
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine()
                .AppendLine(message);
            Console.Write(str);
        }

        void ShowPrompt()
        {
            Console.WriteLine();
            Console.Write(">");
        }

        public bool AcceptIncommingConnection()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine()
                .AppendLine("Accept peer connection? (y/n)");
            Console.WriteLine(str.ToString());

            string response;
            response = Console.ReadLine();

            if (!string.IsNullOrEmpty(response) &&
                (response[0] == 'y' || response[0] == 'Y'))
            {
                return true;
            }

            return false;
        }

        void ShowHelp()
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine()
                .AppendLine("Wi-Fi Direct Demo Usage:")
                .AppendLine("----------------------------------")
                .AppendLine("start             : Start the legacy AP to accept connections")
                .AppendLine("stop              : Stop the legacy AP")
                .AppendLine("info              : Show current Wi-Fi hotspot information")
                .AppendLine("interfaces        : Show network interfaces information")
                .AppendLine("ssid <ssid>       : Configure the SSID before starting the legacy AP")
                .AppendLine("pass <passphrase> : Configure the passphrase before starting the legacy AP")
                .AppendLine("autoaccept <0|1>  : Configure the legacy AP to accept connections (default) or prompt the user")
                .AppendLine("quit|exit         : Exit");
            Console.Write(str);
        }

        bool ExecuteCommand(string command)
        {
            // Simple command parsing logic

            if (command == "quit" ||
                command == "exit")
            {
                Console.WriteLine();
                Console.WriteLine("Exiting");
                return false;
            }
            else if (command == "start")
            {

                Console.WriteLine();
                Console.WriteLine("Starting soft AP...");
                _hostedNetwork.Start();
                _evtObj.WaitOne();
                _evtObj.Reset();
            }
            else if (command == "stop")
            {
                Console.WriteLine();
                Console.WriteLine("Stopping soft AP...");
                _hostedNetwork.Stop();
                _evtObj.WaitOne();
                _evtObj.Reset();
            }
            else if (command.Contains("ssid") && command.Substring(0, 4) == "ssid")
            {
                bool isBadInput = true;
                // Parse the SSID as the first non-space character after ssid
                if (command.Length > 4)
                {
                    string ssid = command.Substring(5).TrimStart(' ');
                    if (!string.IsNullOrEmpty(ssid))
                    {
                        StringBuilder str = new StringBuilder();
                        str.AppendLine()
                            .Append("Setting SSID to ")
                            .AppendLine(ssid);
                        Console.Write(str);
                        _hostedNetwork.SSID = ssid;
                        isBadInput = false;
                    }
                }
                if (isBadInput)
                {
                    Console.WriteLine();
                    Console.WriteLine("Setting SSID FAILED, bad input");
                }
            }
            else if (command.Contains("pass") && command.Substring(0, 4) == "pass")
            {
                bool isBadInput = true;
                // Parse the Passphrase as the first non-space character after pass
                if (command.Length > 4)
                {
                    string pass = command.Substring(5).TrimStart(' ');
                    if (!string.IsNullOrEmpty(pass))
                    {
                        StringBuilder str = new StringBuilder();
                        str.AppendLine()
                            .Append("Setting Passphrase to ")
                            .AppendLine(pass);
                        Console.Write(str);
                        _hostedNetwork.PassPhrase = pass;

                        isBadInput = false;
                    }
                }
                if (isBadInput)
                {
                    Console.WriteLine();
                    Console.WriteLine("Setting Passphrase FAILED, bad input");
                }
            }
            else if (command.Contains("autoaccept") && command.Substring(0, 10) == "autoaccept")
            {
                bool isBadInput = true;
                if (command.Length > 10)
                {
                    string autoAcceptStr = command.Substring(11).TrimStart(' ');
                    if (!string.IsNullOrEmpty(autoAcceptStr))
                    {
                        int acceptValue = 1;
                        if (int.TryParse(autoAcceptStr, out acceptValue))
                        {
                            bool autoAccept = acceptValue == 0 ? false : true;

                            StringBuilder str = new StringBuilder();
                            str.AppendLine()
                                .Append("Setting AutoAccept to ")
                                .Append(autoAccept)
                                .Append(" (input was ")
                                .Append(acceptValue)
                                .AppendLine(")");
                            Console.Write(str);

                            _hostedNetwork.AutoAccept = autoAccept;

                            isBadInput = false;
                        }
                    }
                }
                if (isBadInput)
                {
                    Console.WriteLine();
                    Console.WriteLine("Setting AutoAccpet FAILED, bad input");
                }
            }
            else if (command == "info")
            {
                Console.WriteLine();
                Console.WriteLine(_hostedNetwork.ToString());
            }
            else if (command == "interfaces")
            {
                Console.WriteLine();
                ShowNetworkInterfaces();
            }
            else
            {
                ShowHelp();
            }

            return true;
        }

        public static void ShowNetworkInterfaces()
        {
            IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            Console.WriteLine("Interface information for {0}.{1}     ",
                    computerProperties.HostName, computerProperties.DomainName);
            if (nics == null || nics.Length < 1)
            {
                Console.WriteLine("  No network interfaces found.");
                return;
            }

            Console.WriteLine("  Number of interfaces .................... : {0}", nics.Length);
            foreach (NetworkInterface adapter in nics)
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                Console.WriteLine();
                Console.WriteLine(adapter.Description);
                Console.WriteLine(String.Empty.PadLeft(adapter.Description.Length, '='));
                Console.WriteLine("  Interface type .......................... : {0}", adapter.NetworkInterfaceType);
                Console.WriteLine("  Physical Address ........................ : {0}",
                           adapter.GetPhysicalAddress().ToString());
                Console.WriteLine("  Operational status ...................... : {0}",
                    adapter.OperationalStatus);
                string versions = "";

                // Create a display string for the supported IP versions.
                if (adapter.Supports(NetworkInterfaceComponent.IPv4))
                {
                    versions = "IPv4";
                }
                if (adapter.Supports(NetworkInterfaceComponent.IPv6))
                {
                    if (versions.Length > 0)
                    {
                        versions += " ";
                    }
                    versions += "IPv6";
                }
                Console.WriteLine("  IP version .............................. : {0}", versions);
                ShowIPAddresses(properties);

                // The following information is not useful for loopback adapters.
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                Console.WriteLine("  DNS suffix .............................. : {0}",
                    properties.DnsSuffix);

                string label;
                if (adapter.Supports(NetworkInterfaceComponent.IPv4))
                {
                    IPv4InterfaceProperties ipv4 = properties.GetIPv4Properties();
                    Console.WriteLine("  MTU...................................... : {0}", ipv4.Mtu);
                    if (ipv4.UsesWins)
                    {

                        IPAddressCollection winsServers = properties.WinsServersAddresses;
                        if (winsServers.Count > 0)
                        {
                            label = "  WINS Servers ............................ :";
                        }
                    }
                }

                Console.WriteLine("  DNS enabled ............................. : {0}",
                    properties.IsDnsEnabled);
                Console.WriteLine("  Dynamically configured DNS .............. : {0}",
                    properties.IsDynamicDnsEnabled);
                Console.WriteLine("  Receive Only ............................ : {0}",
                    adapter.IsReceiveOnly);
                Console.WriteLine("  Multicast ............................... : {0}",
                    adapter.SupportsMulticast);

                Console.WriteLine();
            }
        }

        public static void ShowIPAddresses(IPInterfaceProperties adapterProperties)
        {
            IPAddressCollection dnsServers = adapterProperties.DnsAddresses;
            if (dnsServers != null)
            {
                foreach (IPAddress dns in dnsServers)
                {
                    Console.WriteLine("  DNS Servers ............................. : {0}",
                        dns.ToString()
                   );
                }
            }
            IPAddressInformationCollection anyCast = adapterProperties.AnycastAddresses;
            if (anyCast != null)
            {
                foreach (IPAddressInformation any in anyCast)
                {
                    Console.WriteLine("  Anycast Address .......................... : {0} {1} {2}",
                        any.Address,
                        any.IsTransient ? "Transient" : "",
                        any.IsDnsEligible ? "DNS Eligible" : ""
                    );
                }
                Console.WriteLine();
            }

            MulticastIPAddressInformationCollection multiCast = adapterProperties.MulticastAddresses;
            if (multiCast != null)
            {
                foreach (IPAddressInformation multi in multiCast)
                {
                    Console.WriteLine("  Multicast Address ....................... : {0} {1} {2}",
                        multi.Address,
                        multi.IsTransient ? "Transient" : "",
                        multi.IsDnsEligible ? "DNS Eligible" : ""
                    );
                }
                Console.WriteLine();
            }
            UnicastIPAddressInformationCollection uniCast = adapterProperties.UnicastAddresses;
            if (uniCast != null)
            {
                string lifeTimeFormat = "dddd, MMMM dd, yyyy  hh:mm:ss tt";
                foreach (UnicastIPAddressInformation uni in uniCast)
                {
                    DateTime when;

                    Console.WriteLine("  Unicast Address ......................... : {0}", uni.Address);
                    Console.WriteLine("     Prefix Origin ........................ : {0}", uni.PrefixOrigin);
                    Console.WriteLine("     Suffix Origin ........................ : {0}", uni.SuffixOrigin);
                    Console.WriteLine("     Duplicate Address Detection .......... : {0}",
                        uni.DuplicateAddressDetectionState);

                    // Format the lifetimes as Sunday, February 16, 2003 11:33:44 PM
                    // if en-us is the current culture.

                    // Calculate the date and time at the end of the lifetimes.    
                    when = DateTime.UtcNow + TimeSpan.FromSeconds(uni.AddressValidLifetime);
                    when = when.ToLocalTime();
                    Console.WriteLine("     Valid Life Time ...................... : {0}",
                        when.ToString(lifeTimeFormat, System.Globalization.CultureInfo.CurrentCulture)
                    );
                    when = DateTime.UtcNow + TimeSpan.FromSeconds(uni.AddressPreferredLifetime);
                    when = when.ToLocalTime();
                    Console.WriteLine("     Preferred life time .................. : {0}",
                        when.ToString(lifeTimeFormat, System.Globalization.CultureInfo.CurrentCulture)
                    );

                    when = DateTime.UtcNow + TimeSpan.FromSeconds(uni.DhcpLeaseLifetime);
                    when = when.ToLocalTime();
                    Console.WriteLine("     DHCP Leased Life Time ................ : {0}",
                        when.ToString(lifeTimeFormat, System.Globalization.CultureInfo.CurrentCulture)
                    );
                }
                Console.WriteLine();
            }
        }

    }
}
