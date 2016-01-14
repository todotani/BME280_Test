using System;
using System.Threading;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;
using Sensor.BME280;


namespace BME280_Test
{
    public sealed partial class MainPage : Page
    {
        private BME280 bme280;
        private Timer periodicTimer;

        public MainPage()
        {
            this.InitializeComponent();

            bme280 = new BME280();
            initBme280();
        }

        private async void initBme280()
        {
            await bme280.Initialize();
            periodicTimer = new Timer(this.TimerCallback, null, 0, 1000);
        }

        private void TimerCallback(object state)
        {
            var temp = bme280.ReadTemperature();
            var press = bme280.ReadPreasure() / 100;
            var humidity = bme280.ReadHumidity();
            var alt = bme280.ReadAltitude(1013);   // 1013hPa = pressure at 0m
            Debug.WriteLine("Temp:{0:F2}℃ Humidity:{1:F2}% Press:{2:F2}hPa Alt:{3:F0}m", temp, humidity, press, alt);

            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                tempValue.Text  = temp.ToString("F2") + "℃";
                humValue.Text   = humidity.ToString("F2") + "%";
                pressValue.Text = press.ToString("F2") + "hPa";
            });
        }
    }
}
