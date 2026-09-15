using System.Collections.Generic;
using UnityEngine;
using Ultraleap.Haptics;
using System;

namespace HapE.Unity
{
    public class HapEMultiDeviceManager : HapEDeviceManager
    {
        public int numDevicesRequired = 2;
        public List<HapEDevice> hapeDevices;

        [Tooltip("This is the active Hap-e Device that can fire haptics. We only allow one device to fire at any point in time.")]
        public HapEDevice ActiveHapEDevice;
        public GameObject trackingOrigin;
        public TrackingFixation trackingFixation;
        private void Awake()
        {
            autoConnect = false;
        }

        public void SetActiveHapeDevice(HapEDevice hapeDevice)
        {
            if (hapticDevice != hapeDevice.device)
            {
                ActiveHapEDevice = hapeDevice;

                // We can only ever have one device to play back on...
                hapticDevice = ActiveHapEDevice.device;
                Debug.Log("Active HapEDevice is: " + hapeDevice.nickname);
                trackTransformObject.activeHapEDevice = hapeDevice;
                hapeDevice.SetParticleRendererTransform();
                hapticRenderer.simulationSpace = hapeDevice.particleRendererTransform;
                hapticRenderer.SetCustomSimulationSpace();
                trackingFixation.transform.SetParent(hapeDevice.deviceTransform);
                trackingOrigin.transform.SetParent(hapeDevice.deviceTransform);
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            if (autoConnect)
            {
                int connectedDeviceCount = 0;
                List<DeviceInfo> deviceInfos = GetConnectedDevices();
                for (int count = 0; count < deviceInfos.Count; count++)
                {
                    try
                    {
                        Device newDevice = deviceInfos[count].Open();
                        Debug.Log($"NEW Device found : {newDevice.SerialNumber}");
                        string supportedMessage = newDevice.HapE.IsSupported ? "Hap-e Supported" : "Hap-e NOT supported";
                        Debug.Log(supportedMessage);
                        hapeDevices[count].device = newDevice;
                        hapeDevices[count].deviceId = newDevice.SerialNumber;
                        connectedDeviceCount++;
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Exception: " + e.ToString());
                    }
                }

                if (connectedDeviceCount > 0)
                {
                    SetActiveHapeDevice(hapeDevices[0]);
                }
                if (connectedDeviceCount < numDevicesRequired)
                {
                    Debug.LogWarning("Unable to obtain the required number of devices of: " + numDevicesRequired + ". Got: " + connectedDeviceCount);
                }
            }
        }

        /// <summary>
        /// Returns a Hap-e Devivce by a given serial number
        /// </summary>
        /// <returns></returns>
        public Device GetDeviceBySerialNumber(string serialNumber)
        {
            Debug.Log("Attempting to get Device with SerialNumber: " + serialNumber);
            List<DeviceInfo> deviceInfos = GetConnectedDevices();
            List<string> serialsAvailable = new();
            for (int count = 0; count < deviceInfos.Count; count++)
            {
                try
                {
                    Device newDevice = deviceInfos[count].Open();
                    //string supportedMessage = newDevice.HapE.IsSupported ? "Hap-e Supported" : "Hap-e NOT supported";
                    //Debug.Log(supportedMessage);
                    string serialFound = newDevice.SerialNumber;
                    serialsAvailable.Add(serialFound);
                    if (serialFound == serialNumber)
                    {
                        Debug.Log($"Requested device found, obtaining device with Serial: {newDevice.SerialNumber}");
                        return newDevice;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("Exception: " + e.ToString());
                }
            }

            // If we got here, we failed to return a Device with Serial number....
            Debug.LogWarning("GetDeviceBySerialNumber, device with Serial NOT found. Requested serial was: " + serialNumber);
            Debug.LogWarning("Check connected device serials. Devices connected were:\n");
            foreach (string serial in serialsAvailable)
            {
                Debug.LogWarning("Serial found:" + serial);
            }
            return null;
        }

        public override void StopHaptics()
        {
            Debug.Log("HapEMultiDevice, StopHaptics");
            hapticRenderer.ClearParticles();
            foreach (HapEDevice hapeDevice in hapeDevices)
            {
                // We have to wrap this in a try catch, because if the device gets disconnected,
                // there's no signal to allow us to null the hapticDevice.
                try
                {
                    hapeDevice.device?.HapE.Stop();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error Stopping {0}. Message = {1}", hapeDevice.device, e.Message);
                    Debug.LogWarning("Unable to Stop Haptics... Was the device disconnected?");
                }
            }
        }
    }
}