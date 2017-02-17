﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Shared.Windows;

namespace WiinUPro.Windows
{
    /// <summary>
    /// Interaction logic for SyncWindow.xaml
    /// </summary>
    public partial class SyncWindow : Window
    {
        bool cancelled = false;

        public SyncWindow()
        {
            InitializeComponent();
        }

        public void Sync()
        {
            Guid HIDServiceClass = Guid.Parse(NativeImports.HID_GUID);
            List<IntPtr> btRadios = new List<IntPtr>();
            int pairedCount = 0;
            IntPtr foundRadio;
            IntPtr handle;
            NativeImports.Bluetooth_Find_Radio_Params radioParams = new NativeImports.Bluetooth_Find_Radio_Params();

            radioParams.Initialize();

            handle = NativeImports.BluetoothFindFirstRadio(ref radioParams, out foundRadio);

            if (foundRadio != IntPtr.Zero)
            {
                btRadios.Add(foundRadio);
            }

            // TODO: More Radios

            if (handle != IntPtr.Zero)
            {
                Prompt("Searching...");

                while (pairedCount == 0 && !cancelled)
                {
                    for (int r = 0; r < btRadios.Count; r++)
                    {
                        IntPtr found;
                        NativeImports.Bluetooth_Radio_Info radioInfo = new NativeImports.Bluetooth_Radio_Info();
                        NativeImports.BluetoothDeviceInfo deviceInfo = new NativeImports.BluetoothDeviceInfo();
                        NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams = new NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS();

                        radioInfo.Initialize();
                        deviceInfo.Initialize();
                        searchParams.Initialize();

                        uint getInfoError = NativeImports.BluetoothGetRadioInfo(btRadios[r], ref radioInfo);
                        
                        // Success
                        if (getInfoError == 0)
                        {
                            searchParams.fReturnAuthenticated = false;
                            searchParams.fReturnRemembered = false;
                            searchParams.fReturnConnected = false;
                            searchParams.fReturnUnknown = true;
                            searchParams.fIssueInquiry = true;
                            searchParams.cTimeoutMultiplier = 2;
                            searchParams.hRadio = btRadios[r];

                            found = NativeImports.BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);

                            if (found != IntPtr.Zero)
                            {
                                do
                                {
                                    if (deviceInfo.szName.StartsWith("Nintendo RVL"))
                                    {
                                        Prompt("Found " + deviceInfo.szName);

                                        StringBuilder password = new StringBuilder();
                                        uint pcService = 16;
                                        Guid[] guids = new Guid[16];
                                        bool success = true;

                                        if (deviceInfo.fRemembered)
                                        {
                                            // Remove current pairing
                                            uint errForget = NativeImports.BluetoothRemoveDevice(ref deviceInfo.Address);
                                            success = errForget == 0;
                                        }

                                        // use MAC address of BT radio as pin
                                        var bytes = BitConverter.GetBytes(radioInfo.address);
                                        for (int i = 0; i < 6; i++)
                                        {
                                            password.Append((char)bytes[i]);
                                        }

                                        if (success)
                                        {
                                            var errPair = NativeImports.BluetoothAuthenticateDevice(IntPtr.Zero, btRadios[r], ref deviceInfo, password.ToString(), 6);
                                            success = errPair == 0;
                                        }

                                        if (success)
                                        {
                                            var errService = NativeImports.BluetoothEnumerateInstalledServices(btRadios[r], ref deviceInfo, ref pcService, guids);
                                            success = errService == 0;
                                        }

                                        if (success)
                                        {
                                            var errActivate = NativeImports.BluetoothSetServiceState(btRadios[r], ref deviceInfo, ref HIDServiceClass, 0x01);
                                            success = errActivate == 0;
                                        }

                                        if (success)
                                        {
                                            Prompt("Successfully Paired!");
                                            pairedCount += 1;
                                        }
                                        else
                                        {
                                            Prompt("Failed to Pair.");
                                        }
                                    }
                                }
                                while (NativeImports.BluetoothFindNextDevice(found, ref deviceInfo));
                            }
                        }
                        else
                        {
                            // Failed to get Bluetooth Radio Info
                            Prompt("Failed to get Bluetooth Radio Info");
                        }
                    }
                }

                // Close Opened Radios
                foreach (var openRadio in btRadios)
                {
                    NativeImports.CloseHandle(openRadio);
                }
            }
            else
            {
                // No Bluetooth Radios found
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    status.Text = "No Bluetooth Radios Found.";
                }));
            }

            Dispatcher.BeginInvoke((Action)(() => Close()));
        }

        private void Prompt(string text)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                status.Text += text + Environment.NewLine;
            }));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task t = new Task(() => Sync());
            t.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cancelled = true;
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            Prompt("Cancelling");
            cancelled = true;
        }
    }
}