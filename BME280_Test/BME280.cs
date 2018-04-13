// MIT License
// Original Source: https://github.com/ms-iot/adafruitsample/tree/master/Lesson_203V2/FullSolution

using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace Sensor.BME280
{
    public class BME280_CalibrationData
    {
        //BME280 Registers
        public ushort dig_T1 { get; set; }
        public short  dig_T2 { get; set; }
        public short  dig_T3 { get; set; }

        public ushort dig_P1 { get; set; }
        public short  dig_P2 { get; set; }
        public short  dig_P3 { get; set; }
        public short  dig_P4 { get; set; }
        public short  dig_P5 { get; set; }
        public short  dig_P6 { get; set; }
        public short  dig_P7 { get; set; }
        public short  dig_P8 { get; set; }
        public short  dig_P9 { get; set; }

        public byte   dig_H1 { get; set; }
        public short  dig_H2 { get; set; }
        public byte   dig_H3 { get; set; }
        public short  dig_H4 { get; set; }
        public short  dig_H5 { get; set; }
        public sbyte  dig_H6 { get; set; }
    }


    public class BME280
    {
        const byte BME280_Address   = 0x76;
        const byte BME280_Signature = 0x60;

        enum eRegisters : byte
        {
            BME280_REGISTER_DIG_T1 = 0x88,
            BME280_REGISTER_DIG_T2 = 0x8A,
            BME280_REGISTER_DIG_T3 = 0x8C,

            BME280_REGISTER_DIG_P1 = 0x8E,
            BME280_REGISTER_DIG_P2 = 0x90,
            BME280_REGISTER_DIG_P3 = 0x92,
            BME280_REGISTER_DIG_P4 = 0x94,
            BME280_REGISTER_DIG_P5 = 0x96,
            BME280_REGISTER_DIG_P6 = 0x98,
            BME280_REGISTER_DIG_P7 = 0x9A,
            BME280_REGISTER_DIG_P8 = 0x9C,
            BME280_REGISTER_DIG_P9 = 0x9E,

            BME280_REGISTER_DIG_H1 = 0xA1,
            BME280_REGISTER_DIG_H2 = 0xE1,
            BME280_REGISTER_DIG_H3 = 0xE3,
            BME280_REGISTER_DIG_H4_L = 0xE4,
            BME280_REGISTER_DIG_H4_H = 0xE5,
            BME280_REGISTER_DIG_H5_L = 0xE5,
            BME280_REGISTER_DIG_H5_H = 0xE6,
            BME280_REGISTER_DIG_H6 = 0xE7,

            BME280_REGISTER_CHIPID  = 0xD0,
            BME280_REGISTER_SOFTRESET = 0xE0,

            BME280_REGISTER_CONTROLHUMID = 0xF2,
            BME280_REGISTER_STATUS  = 0xF3,
            BME280_REGISTER_CONTROL = 0xF4,
            BME280_REGISTER_CONFIG  = 0xF5,

            BME280_REGISTER_PRESSUREDATA_MSB  = 0xF7,
            BME280_REGISTER_PRESSUREDATA_LSB  = 0xF8,
            BME280_REGISTER_PRESSUREDATA_XLSB = 0xF9, // bits <7:4>

            BME280_REGISTER_TEMPDATA_MSB  = 0xFA,
            BME280_REGISTER_TEMPDATA_LSB  = 0xFB,
            BME280_REGISTER_TEMPDATA_XLSB = 0xFC, // bits <7:4>

            BME280_REGISTER_HUMIDDATA_MSB = 0xFD,
            BME280_REGISTER_HUMIDDATA_LSB = 0xFE,
        };

        // Enables 2-wire I2C interface when set to ‘0’
        public enum interface_mode_e : byte
        {
            i2c = 0,
            spi = 1
        };

        // t_sb standby options - effectively the gap between automatic measurements 
        // when in "normal" mode
        public enum standbySettings_e : byte
        {
            tsb_0p5ms   = 0,
            tsb_62p5ms  = 1,
            tsb_125ms   = 2,
            tsb_250ms   = 3,
            tsb_500ms   = 4,
            tsb_1000ms  = 5,
            tsb_10ms    = 6,
            tsb_20ms    = 7
        };


        // sensor modes, it starts off in sleep mode on power on
        // forced is to take a single measurement now
        // normal takes measurements reqularly automatically
        public enum mode_e :byte
        {
            smSleep     = 0,
            smForced    = 1,
            smNormal    = 3
        };


        // Filter coefficients
        // higher numbers slow down changes, such as slamming doors
        public enum filterCoefficient_e : byte
        {
            fc_off  = 0,
            fc_2    = 1,
            fc_4    = 2,
            fc_8    = 3,
            fc_16   = 4
        };


        // Oversampling options for humidity
        // Oversampling reduces the noise from the sensor
        public enum oversampling_e : byte
        {
            osSkipped   = 0,
            os1x        = 1,
            os2x        = 2,
            os4x        = 3,
            os8x        = 4,
            os16x       = 5
        };


        //String for the friendly name of the I2C bus 
        private const string I2CControllerName = "I2C1";
        //Create an I2C device
        private I2cDevice bme280 = null;
        //Create new calibration data for the sensor
        private BME280_CalibrationData CalibrationData;

        // Value hold sensor operation parameters
        private byte int_mode = (byte)interface_mode_e.i2c;
        private byte t_sb;
        private byte mode;
        private byte filter;
        private byte osrs_p;
        private byte osrs_t;
        private byte osrs_h;

        public BME280(standbySettings_e t_sb = standbySettings_e.tsb_0p5ms, 
                      mode_e mode = mode_e.smNormal, 
                      filterCoefficient_e filter = filterCoefficient_e.fc_16,
                      oversampling_e osrs_p = oversampling_e.os16x, 
                      oversampling_e osrs_t = oversampling_e.os2x,
                      oversampling_e osrs_h = oversampling_e.os1x)
        {
            this.t_sb = (byte)t_sb;
            this.mode = (byte)mode;
            this.filter = (byte)filter;
            this.osrs_p = (byte)osrs_p;
            this.osrs_t = (byte)osrs_t;
            this.osrs_h = (byte)osrs_h;
        }


        //Method to initialize the BME280 sensor
        public async Task Initialize()
        {
            Debug.WriteLine("BME280::Initialize");

            try
            {
                //Instantiate the I2CConnectionSettings using the device address of the BME280
                I2cConnectionSettings settings = new I2cConnectionSettings(BME280_Address);
                //Set the I2C bus speed of connection to fast mode
                settings.BusSpeed = I2cBusSpeed.FastMode;
                //Use the I2CBus device selector to create an advanced query syntax string
                string aqs = I2cDevice.GetDeviceSelector(I2CControllerName);
                //Use the Windows.Devices.Enumeration.DeviceInformation class to create a collection using the advanced query syntax string
                DeviceInformationCollection dis = await DeviceInformation.FindAllAsync(aqs);
                //Instantiate the the BME280 I2C device using the device id of the I2CBus and the I2CConnectionSettings
                bme280 = await I2cDevice.FromIdAsync(dis[0].Id, settings);
                //Check if device was found
                if (bme280 == null)
                {
                    Debug.WriteLine("Device not found");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\n" + e.StackTrace);
                throw;
            }

            byte[] readChipID = new byte[] { (byte)eRegisters.BME280_REGISTER_CHIPID };
            byte[] ReadBuffer = new byte[] { 0xFF };

            //Read the device signature
            bme280.WriteRead(readChipID, ReadBuffer);
            Debug.WriteLine("BME280 Signature: " + ReadBuffer[0].ToString());

            //Verify the device signature
            if (ReadBuffer[0] != BME280_Signature)
            {
                Debug.WriteLine("BME280::Begin Signature Mismatch.");
                return;
            }

            //Read the coefficients table
            CalibrationData = ReadCoefficeints();

            //Set configuration registers
            WriteConfigRegister();
            WriteControlMeasurementRegister();
            WriteControlRegisterHumidity();

            //Set configuration registers again to ensure configuration of humidity
            WriteConfigRegister();
            WriteControlMeasurementRegister();
            WriteControlRegisterHumidity();

            //Dummy read temp to setup t_fine
            ReadTemperature();
        }


        //Method to write the config register (default 16)
        //000  100  00 
        // ↑  ↑   ↑I2C mode
        // ↑  ↑Filter coefficient = 16
        // ↑t_sb = 0.5ms
        private void WriteConfigRegister()
        {
            byte value = (byte)(int_mode + (filter << 2) + (t_sb << 5));
            byte[] WriteBuffer = new byte[] { (byte)eRegisters.BME280_REGISTER_CONFIG, value };
            bme280.Write(WriteBuffer);
            return;
        }

        //Method to write the control measurment register (default 87)
        //010  101  11 
        // ↑  ↑   ↑ mode
        // ↑  ↑ Pressure oversampling
        // ↑ Temperature oversampling
        private void WriteControlMeasurementRegister()
        {
            byte value = (byte)(mode + (osrs_p << 2) + (osrs_t << 5));
            byte[] WriteBuffer = new byte[] { (byte)eRegisters.BME280_REGISTER_CONTROL, value };
            bme280.Write(WriteBuffer);
            return;
        }

        //Method to write the humidity control register (default 01)
        private void WriteControlRegisterHumidity()
        {
            byte value = osrs_h;
            byte[] WriteBuffer = new byte[] { (byte)eRegisters.BME280_REGISTER_CONTROLHUMID, value };
            bme280.Write(WriteBuffer);
            return;
        }


        //Method to read a 16-bit value from a register and return it in little endian format
        private ushort ReadUInt16_LittleEndian(byte register)
        {
            ushort value = 0;
            byte[] writeBuffer = new byte[] { 0x00 };
            byte[] readBuffer = new byte[] { 0x00, 0x00 };

            writeBuffer[0] = register;

            bme280.WriteRead(writeBuffer, readBuffer);
            int h = readBuffer[1] << 8;
            int l = readBuffer[0];
            value = (ushort)(h + l);
            return value;
        }

        //Method to read an 8-bit value from a register
        private byte ReadByte(byte register)
        {
            byte value = 0;
            byte[] writeBuffer = new byte[] { 0x00 };
            byte[] readBuffer = new byte[] { 0x00 };

            writeBuffer[0] = register;

            bme280.WriteRead(writeBuffer, readBuffer);
            value = readBuffer[0];
            return value;
        }

        //Method to read the caliberation data from the registers
        private BME280_CalibrationData ReadCoefficeints()
        {
            // 16 bit calibration data is stored as Little Endian, the helper method will do the byte swap.
            CalibrationData = new BME280_CalibrationData();

            // Read temperature calibration data
            CalibrationData.dig_T1 = ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_T1);
            CalibrationData.dig_T2 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_T2);
            CalibrationData.dig_T3 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_T3);

            // Read presure calibration data
            CalibrationData.dig_P1 = ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P1);
            CalibrationData.dig_P2 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P2);
            CalibrationData.dig_P3 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P3);
            CalibrationData.dig_P4 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P4);
            CalibrationData.dig_P5 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P5);
            CalibrationData.dig_P6 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P6);
            CalibrationData.dig_P7 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P7);
            CalibrationData.dig_P8 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P8);
            CalibrationData.dig_P9 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_P9);

            // Read humidity calibration data
            CalibrationData.dig_H1 = ReadByte((byte)eRegisters.BME280_REGISTER_DIG_H1);
            CalibrationData.dig_H2 = (short)ReadUInt16_LittleEndian((byte)eRegisters.BME280_REGISTER_DIG_H2);
            CalibrationData.dig_H3 = ReadByte((byte)eRegisters.BME280_REGISTER_DIG_H3);
            short e4 = ReadByte((byte)eRegisters.BME280_REGISTER_DIG_H4_L);    // Read 0xE4
            short e5 = ReadByte((byte)eRegisters.BME280_REGISTER_DIG_H4_H);    // Read 0xE5
            CalibrationData.dig_H4 = (short)((e4 << 4) + (e5 & 0x0F));
            short e6 = ReadByte((byte)eRegisters.BME280_REGISTER_DIG_H5_H);    // Read 0xE6
            CalibrationData.dig_H5 = (short)((e5 >> 4) + (e6 << 4));
            CalibrationData.dig_H6 = (sbyte)ReadByte((byte)eRegisters.BME280_REGISTER_DIG_H6);

            return CalibrationData;
        }


        //t_fine carries fine temperature as global value
        int t_fine;

        //Method to return the temperature in DegC. Resolution is 0.01 DegC. Output value of “51.23” equals 51.23 DegC.
        private double BME280_compensate_T_double(int adc_T)
        {
            double var1, var2, T;

            //The temperature is calculated using the compensation formula in the BME280 datasheet
            var1 = (adc_T / 16384.0 - CalibrationData.dig_T1 / 1024.0) * CalibrationData.dig_T2;
            var2 = ((adc_T / 131072.0 - CalibrationData.dig_T1 / 8192.0) * 
                (adc_T / 131072.0 - CalibrationData.dig_T1 / 8192.0)) * CalibrationData.dig_T3;

            t_fine = (int)(var1 + var2);

            T = (var1 + var2) / 5120.0;
            return T;
        }


        //Method to returns the pressure in Pa, in Q24.8 format (24 integer bits and 8 fractional bits).
        //Output value of “24674867” represents 24674867/256 = 96386.2 Pa = 963.862 hPa
        private long BME280_compensate_P_Int64(int adc_P)
        {
            long var1, var2, p;

            //The pressure is calculated using the compensation formula in the BME280 datasheet
            var1 = (long)t_fine - 128000;
            var2 = var1 * var1 * CalibrationData.dig_P6;
            var2 = var2 + ((var1 * CalibrationData.dig_P5) << 17);
            var2 = var2 + ((long)CalibrationData.dig_P4 << 35);
            var1 = ((var1 * var1 * CalibrationData.dig_P3) >> 8) + ((var1 * CalibrationData.dig_P2) << 12);
            var1 = (((long)1 << 47) + var1) * CalibrationData.dig_P1 >> 33;
            if (var1 == 0)
            {
                Debug.WriteLine("BME280_compensate_P_Int64 Jump out to avoid / 0");
                return 0; //Avoid exception caused by division by zero
            }
            //Perform calibration operations as per datasheet: 
            p = 1048576 - adc_P;
            p = (((p << 31) - var2) * 3125) / var1;
            var1 = ((long)CalibrationData.dig_P9 * (p >> 13) * (p >> 13)) >> 25;
            var2 = ((long)CalibrationData.dig_P8 * p) >> 19;
            p = ((p + var1 + var2) >> 8) + ((long)CalibrationData.dig_P7 << 4);
            return p;
        }


        // Returns humidity in %rH as as double. Output value of “46.332” represents 46.332 %rH
        private double BME280_compensate_H_double(int adc_H)
        {
            double var_H;

            var_H = t_fine - 76800.0;
            var_H = (adc_H - (CalibrationData.dig_H4 * 64.0 + CalibrationData.dig_H5 / 16384.0 * var_H)) *
                CalibrationData.dig_H2 / 65536.0 * (1.0 + CalibrationData.dig_H6 / 67108864.0 * var_H *
                (1.0 + CalibrationData.dig_H3 / 67108864.0 * var_H));
            var_H = var_H * (1.0 - CalibrationData.dig_H1 * var_H / 524288.0);

            if (var_H > 100.0)
            {
                Debug.WriteLine("BME280_compensate_H_double Jump out to 100%");
                var_H = 100.0;
            } else if (var_H < 0.0)
            {
                Debug.WriteLine("BME280_compensate_H_double Jump under 0%");
                var_H = 0.0;
            }

            return var_H;
        }


        public float ReadTemperature()
        {
            //Read the MSB, LSB and bits 7:4 (XLSB) of the temperature from the BME280 registers
            byte tmsb = ReadByte((byte)eRegisters.BME280_REGISTER_TEMPDATA_MSB);
            byte tlsb = ReadByte((byte)eRegisters.BME280_REGISTER_TEMPDATA_LSB);
            byte txlsb = ReadByte((byte)eRegisters.BME280_REGISTER_TEMPDATA_XLSB); // bits 7:4

            //Combine the values into a 32-bit integer
            int t = (tmsb << 12) + (tlsb << 4) + (txlsb >> 4);

            //Convert the raw value to the temperature in degC
            double temp = BME280_compensate_T_double(t);

            //Return the temperature as a float value
            return (float)temp;
        }

        public float ReadPreasure()
        {
            //Read the MSB, LSB and bits 7:4 (XLSB) of the pressure from the BME280 registers
            byte pmsb = ReadByte((byte)eRegisters.BME280_REGISTER_PRESSUREDATA_MSB);
            byte plsb = ReadByte((byte)eRegisters.BME280_REGISTER_PRESSUREDATA_LSB);
            byte pxlsb = ReadByte((byte)eRegisters.BME280_REGISTER_PRESSUREDATA_XLSB); // bits 7:4

            //Combine the values into a 32-bit integer
            int p = (pmsb << 12) + (plsb << 4) + (pxlsb >> 4);

            //Convert the raw value to the pressure in Pa
            long pres = BME280_compensate_P_Int64(p);

            //Return the pressure as a float value
            return ((float)pres) / 256;
        }

        public float ReadHumidity()
        {
            //Read the MSB and LSB of the humidity from the BME280 registers
            byte hmsb = ReadByte((byte)eRegisters.BME280_REGISTER_HUMIDDATA_MSB);
            byte hlsb = ReadByte((byte)eRegisters.BME280_REGISTER_HUMIDDATA_LSB);

            //Combine the values into a 32-bit integer
            int h = (hmsb << 8) + hlsb;

            //Convert the raw value to the humidity in %
            double humidity = BME280_compensate_H_double(h);

            //Return the humidity as a float value
            return (float)humidity;
        }

        //Method to take the sea level pressure in Hectopascals(hPa) as a parameter and calculate the altitude using current pressure.
        public float ReadAltitude(float seaLevel)
        {
            //Read the pressure first
            float pressure = ReadPreasure();
            //Convert the pressure to Hectopascals(hPa)
            pressure /= 100;

            //Calculate and return the altitude using the international barometric formula
            return 44330.0f * (1.0f - (float)Math.Pow((pressure / seaLevel), 0.1903f));
        }
    }
}
