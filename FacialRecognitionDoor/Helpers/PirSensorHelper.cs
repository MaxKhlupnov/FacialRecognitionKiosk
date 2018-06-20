using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maker.Devices.Gpio.PirSensor;
using Windows.Devices.Gpio;

namespace FacialRecognitionDoor.Helpers
{
    public class PirSensorHelper
    {
        public delegate void PirSensorMotionDetected(object sender, GpioPinValueChangedEventArgs e);
        public PirSensorMotionDetected MotionDetected;
        private PirSensor pirSensor;

        /*******************************************************************************************
       * PUBLIC METHODS
       *******************************************************************************************/
        public bool Initialize()
        {
            var gpioController = GpioController.GetDefault();
            if (gpioController == null)
                return false;

                //Initialize PIR Sensor
             pirSensor = new PirSensor(GpioConstants.PirSensorPinID, PirSensor.SensorType.ActiveHigh);
            pirSensor.motionDetected += PirSensor_motionDetected;
            return true;
        }

        private void PirSensor_motionDetected(object sender, GpioPinValueChangedEventArgs e)
        {
            if (MotionDetected != null)
                MotionDetected.Invoke(sender, e);
        }
    }
}
