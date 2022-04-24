using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;    // Yade2020419

namespace QuickBlueToothLE
{
    class Program
    {
        static DeviceInformation device = null;
        static int MAX_LENGTH = 243;

        public static string HEART_RATE_SERVICE_ID = "180d";                    // Heart Rate Service
        public static string OBJECT_TRANSFER_SERVICE_ID = "1825";               // Object Transfer Service
        public static string OBJECT_NAME_CHARACTERISTIC_ID = "2abe";            // Object Name Charateristic
        public static string OBJECT_LIST_CONTROL_CHARACTERISTIC_ID = "2ac6";     // Object List Control Charateristic

        // Only one registered characteristic at a time.
        private static GattCharacteristic selectedCharacteristic;
        private static GattCharacteristic registeredCharacteristic;
        private static GattPresentationFormat presentationFormat;
        #region Error Codes
        readonly static int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly static int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly static int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly static int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion

        static async Task Main(string[] args)
        {
            // Query for extra properties you want returned
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // This will create a file named sample.txt
            // at the specified location 
            StreamReader sr = new StreamReader(args[0]);
            string str = sr.ReadLine();

            // Start the watcher.
            deviceWatcher.Start();
            while (true)
            {
                if (device == null)
                {
                    Thread.Sleep(200);
                }
                else
                {
                    Console.WriteLine("Press Any to pair with TEST_RBLE");
                    Console.ReadKey();
                    BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                    Console.WriteLine("Attempting to pair with device");
                    GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync();
                    GattReadResult result2;
                    bool bRead = true;
                    while (bRead)
                    {
                        if (result.Status == GattCommunicationStatus.Success)
                        {
//                            Console.WriteLine("Pairing succeeded");
                            var services = result.Services;
                            foreach (var service in services)
                            {
                                //                            Console.WriteLine(service.Uuid);
                                //                           Console.WriteLine(service.DeviceAccessInformation);
                                //                            if (service.Uuid.ToString("N").Substring(4, 4) == HEART_RATE_SERVICE_ID)
                                if (service.Uuid.ToString("N").Substring(4, 4) == OBJECT_TRANSFER_SERVICE_ID)
                                {
                                    //       Console.WriteLine("Found heart rate service");
//                                    Console.WriteLine("Found object transfer service(OTS)");
                                    GattCharacteristicsResult charactiristicResult = await service.GetCharacteristicsAsync();

                                    if (charactiristicResult.Status == GattCommunicationStatus.Success)
                                    {
                                        var characteristics = charactiristicResult.Characteristics;
                                        foreach (var characteristic in characteristics)
                                        {
                                            GattCharacteristicProperties properties = characteristic.CharacteristicProperties;

                                            int lenOfSentData = 0;
                                            if (characteristic.Uuid.ToString("N").Substring(4, 4) == OBJECT_NAME_CHARACTERISTIC_ID)
                                            {
                                                selectedCharacteristic = characteristic;
                                                if (properties.HasFlag(GattCharacteristicProperties.Write))
                                                {
                                                    //                                                    Console.WriteLine("Write property for Object Name found");
                                                    //Console.WriteLine("Enter input(max. 243 chars): ");
                                                    //string sendData = Console.ReadLine();
                                                    string sendData = sr.ReadLine();
                                                    if (sendData == null)
                                                    {
                                                        bRead = false;
                                                        break;
                                                    }
                                                    lenOfSentData = sendData.Length;
                                                    if (lenOfSentData < MAX_LENGTH)
                                                    {
                                                        sendData = sendData.PadRight(MAX_LENGTH - lenOfSentData, ' ');
                                                    }
                                                    else
                                                        sendData = sendData.Substring(0, MAX_LENGTH);

                                                    var writeBuffer = CryptographicBuffer.ConvertStringToBinary(sendData, BinaryStringEncoding.Utf8);

                                                    var writeSuccessful = await WriteBufferToSelectedCharacteristicAsync(writeBuffer);
                                                    //if (writeSuccessful == )

                                                    Console.WriteLine($"Sent data: {sendData}{lenOfSentData} (bytes)");

                                                }
                                                else
                                                    Console.WriteLine("Write property for Object Name NOT found");
                                                // Read data from remote device by object name service
                                                if (properties.HasFlag(GattCharacteristicProperties.Read) && bRead)
                                                {
                                                    // BT_Code: Read the actual value from the device by using Uncached.
                                                    result2 = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                                                    if (result2.Status == GattCommunicationStatus.Success)
                                                    {
                                                        string formattedResult = FormatValueByPresentation(result2.Value, presentationFormat);
                                                        formattedResult = formattedResult.Substring(0, lenOfSentData);
                                                        Console.WriteLine($"Read data: {formattedResult}({lenOfSentData} bytes) ");
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine($"Read failed: {result2.Status}");
                                                    }
                                                }
                                                else
                                                    Console.WriteLine("Read property for Object Name NOT found");
                                            }
                                            //else if (characteristic.Uuid.ToString("N").Substring(4, 4) == OBJECT_LIST_CONTROL_CHARACTERISTIC_ID)
                                            //{
                                            //    Console.WriteLine("Characteristic for Object List Control found");
                                            //    //if (properties.HasFlag(GattCharacteristicProperties.Read))
                                            //    //    Console.WriteLine("Read property for Object  List Control found");
                                            //    //else
                                            //    //    Console.WriteLine("Read property for Object List Control NOT found");

                                            //    if (properties.HasFlag(GattCharacteristicProperties.Write)) {
                                            //        Console.WriteLine("Write property for Object List Control found");
                                            //        Console.WriteLine("Enter input(max. 243 chars): ");
                                            //        string sendData = Console.ReadLine();
                                            //        if (sendData == "exit")
                                            //        {
                                            //            bRead = false;
                                            //            break;
                                            //        }
                                            //        if (sendData.Length < MAX_LENGTH)
                                            //        {
                                            //            sendData = sendData.PadRight(MAX_LENGTH - sendData.Length, ' ');
                                            //        }
                                            //        else
                                            //            sendData = sendData.Substring(0, MAX_LENGTH);

                                            //        var writeBuffer = CryptographicBuffer.ConvertStringToBinary(sendData, BinaryStringEncoding.Utf8);

                                            //        var writeSuccessful = await WriteBufferToSelectedCharacteristicAsync(writeBuffer);

                                            //        Console.WriteLine("Write property for Object  List Control found");
                                            //    } 
                                            //    else
                                            //        Console.WriteLine("Write property for Object List Control NOT found");

                                            //    //if (properties.HasFlag(GattCharacteristicProperties.Notify))
                                            //    //    Console.WriteLine("Notify property for Object  List Control found");
                                            //    //else
                                            //    //    Console.WriteLine("Notify property for Object List Control NOT found");

                                            //    if (properties.HasFlag(GattCharacteristicProperties.Indicate))
                                            //    {
                                            //        Console.WriteLine("Indicate property for Object  List Control found");
                                            //        GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            //                                         GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                                            //        if (status == GattCommunicationStatus.Success)
                                            //        {
                                            //            characteristic.ValueChanged += Characteristic_ValueChanged;
                                            //            // Server has been informed of clients interest.
                                            //        }
                                            //    }
                                            //    else
                                            //        Console.WriteLine("Indicate property for Object List Control NOT found");
                                            //}
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Console.WriteLine("Press Any Key to Exit application");
                    Console.ReadKey();
                    break;
                }
            }
            deviceWatcher.Stop();
        }

        private static async Task<bool> WriteBufferToSelectedCharacteristicAsync(IBuffer buffer)
        {
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await selectedCharacteristic.WriteValueWithResultAsync(buffer);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    Console.WriteLine("Successfully wrote value to device");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Write failed: {result.Status}");
                    return false;
                }
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_INVALID_PDU)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            catch (Exception ex) when (ex.HResult == E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED || ex.HResult == E_ACCESSDENIED)
            {
                // This usually happens when a device reports that it support writing, but it actually doesn't.
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private static string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);
            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                else if (format.FormatType == GattPresentationFormatTypes.Utf8)
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }
                }
                else
                {
                    // Add support for other format types as needed.
                    return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
                }
            }
            else if (data != null)
            {
                // We don't know what format to use. Let's try some well-known profiles, or default back to UTF-8.
                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.HeartRateMeasurement))
                {
                    try
                    {
                        return "Heart Rate: " /*+ ParseHeartRateValue(data).ToString() */;
                    }
                    catch (ArgumentException)
                    {
                        return "Heart Rate: (unable to parse)";
                    }
                }
                else if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                {
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Battery Level: " + data[0].ToString() + "%";
                    }
                    catch (ArgumentException)
                    {
                        return "Battery Level: (unable to parse)";
                    }
                }
                else
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "Unknown format";
                    }
                }
            }
            else
            {
                return "Empty data received";
            }
            return "Unknown format";
        }

        private static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            //var reader = DataReader.FromBuffer(args.CharacteristicValue);
            //var flags = reader.ReadByte();
            //var value = reader.ReadByte();
            //Console.WriteLine($"{flags} - {value}");
            Console.WriteLine("Indicate property for Object List Control received...");
        }

        private static void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (device != null) return;

            Console.WriteLine(args.Name);
            if (args.Name == "TEST_RBLE" || args.Name == "RBLE"/* || args.Name == "FSC-BT958-LE"*/) {  // Mi Smart Band 5
                Console.WriteLine(args.Name);
                device = args;
            }
            //throw new NotImplementedException();
        }
    }
}
