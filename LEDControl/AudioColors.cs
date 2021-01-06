using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NAudio;
using NAudio.Wave;

namespace LEDControl
{
    class AudioColors
    {
        public AudioColors()
        {
            try
            {
                _deviceName = Properties.Settings.Default["LastAudioDevice"] as string;
            }
            catch
            {
                _deviceName = "";
            }
            setup();
        }
        ~AudioColors()
        {
            Properties.Settings.Default["LastAudioDevice"] = _deviceName;
            Properties.Settings.Default.Save();
        }
        private static string _deviceName;
        private static bool _deviceNameChanged = false;
        public static string DeviceName
        {
            get { return _deviceName; }
            set { _deviceName = value; _deviceNameChanged = true; }
        }
        public int fftCount = 4;
        private SampleAggregator[] sampleAggregatorL;
        private SampleAggregator[] sampleAggregatorR;
        int fftLength = 4096;
        public void setup()
        {
            sampleAggregatorL = new SampleAggregator[fftCount];
            sampleAggregatorR = new SampleAggregator[fftCount];
            for (int i = 0; i < fftCount; i++)
            {
                sampleAggregatorL[i] = new SampleAggregator(fftLength);
                sampleAggregatorL[i].FftCalculated += FftCalculatedL;
                sampleAggregatorL[i].PerformFFT = true;
                for (int j = 0; j < fftLength * i / fftCount; j++)
                    sampleAggregatorL[i].Add(0f);

                sampleAggregatorR[i] = new SampleAggregator(fftLength);
                sampleAggregatorR[i].FftCalculated += FftCalculatedR;
                sampleAggregatorR[i].PerformFFT = true;
                for (int j = 0; j < fftLength * i / fftCount; j++)
                    sampleAggregatorR[i].Add(0f);
            }

            //wo = new WaveOut();
            wi = new WaveIn();
            var caps = new List<WaveInCapabilities>();
            int sm_devnum = 0;
            for (int i = 0; i < WaveIn.DeviceCount; i++ )
            {
                var cap = WaveIn.GetCapabilities(i);
                caps.Add(cap);
                if (cap.ProductName.Contains("Stereo Mix"))
                {
                    wi.DeviceNumber = i;
                    sm_devnum = i;
                }
                if(cap.ProductName == DeviceName)
                {
                    wi.DeviceNumber = i;
                    sm_devnum = -1;
                    break;
                }
            }
            if (sm_devnum != -1) wi.DeviceNumber = sm_devnum;
            _deviceName = caps[wi.DeviceNumber].ProductName;
            _deviceNameChanged = false;
            
            /*
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var cap = WaveOut.GetCapabilities(i);
                if (cap.ProductName.Contains("Logitech"))
                    wo.DeviceNumber = i;
            }
             */
            wi.WaveFormat = new WaveFormat(48000, 16, 2);

            wi.DataAvailable += new EventHandler<WaveInEventArgs>(wi_DataAvailable);
            wi.BufferMilliseconds = 25;
            

            //bwp = new BufferedWaveProvider(wi.WaveFormat);
            //bwp.DiscardOnBufferOverflow = true;

            //wo.Init(bwp);
            wi.StartRecording();
            //wo.Play();
        }
        public void stop()
        {
            wi.StopRecording();
            for(int i=0; i<fftCount; i++)
            {
                sampleAggregatorL[i] = null;
                sampleAggregatorR[i] = null;
            }
        }

        public WaveIn wi;
        //WaveOut wo;

        int hob(int num)
        {
            if (num == 0)
                return 0;

            int ret = 1;

            while ((num >>= 1) > 0)
                ret <<= 1;

            return ret;
        }

        void wi_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (LEDSetup.OVERRIDE) return;
            if (_deviceNameChanged)
            {
                stop();
                setup();
                return;
            }
            float masterVol = VolumeUtilities.GetMasterVolume();
            //bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
            //int size = hob(e.BytesRecorded) >> 1;
            //float[] buffer = new float[size];
            for (int i = 0; i < e.BytesRecorded; i+=4)
            {
                //buffer[i] = (float)BitConverter.ToInt16(e.Buffer, i << 1);
                float left = (float)BitConverter.ToInt16(e.Buffer, i) / masterVol;
                //sampleAggregatorL0.Add(left);
                //sampleAggregatorL1.Add(left);

                float right = (float)BitConverter.ToInt16(e.Buffer, i+2) / masterVol;
                //sampleAggregatorR0.Add(right);
                //sampleAggregatorR1.Add(right);
                for (int j = 0; j < fftCount; j++)
                {
                    sampleAggregatorL[j].Add(left);
                    sampleAggregatorR[j].Add(right);
                }
            }
        }

        float[] maxVals = new float[80];
        int maxVali = 0;

        float[] beatVals = new float[80];
        int beatVali = 0;
        bool beatHit = false;
        public int beats;

        void FftCalculatedL(object sender, FftEventArgs e)
        {
            doFft(0, e);
        }
        void FftCalculatedR(object sender, FftEventArgs e)
        {
            doFft(1, e);
        }
        int fftSent = 0;
        byte[] serialData = LEDSetup.getMagicHeader();
        void doFft(int channel, FftEventArgs e)
        {
            // Do something with e.result!
            float[] output = new float[e.Result.Length];
            float val = 0;
            int val_i = 0;
            for (int i = 0; i < e.Result.Length; i++)
            {
                output[i] = (float)Math.Sqrt(e.Result[i].X * e.Result[i].X + e.Result[i].Y * e.Result[i].Y);
                if (output[i] > val)
                {
                    val = output[i];
                    val_i = i;
                }
            }
            maxVals[maxVali++] = output.Max();
            if (maxVali >= maxVals.Length) maxVali = 0;
            float scale = maxVals.Average();
            if (scale < 50) scale = 50;
#if DEBUG
            //Console.WriteLine("{3}: FFT value max of {0} at index {1} ({2} Hz), scale {4}", val, val_i, val_i * wi.WaveFormat.SampleRate / fftLength, DateTime.Now.Millisecond, scale);
#endif
            int maxi = 12000 / (wi.WaveFormat.SampleRate / fftLength);
            int s = maxi / (LEDSetup.LED_C / 2);
            //byte[] serialData = LEDSetup.getMagicHeader();
            /*
            for (int i = 0; i < LEDSetup.LED_C/2; i++)
            {
                float sum = 0;
                for(int j=i; j<i*s;j++){
                    sum+=output[j];
                }
                //sum/=(s);
                sum /= 100;
                LEDSetup.processColor(i, serialData, (int)sum, (int)sum, (int)sum);
                LEDSetup.processColor(24-i, serialData, (int)sum, (int)sum, (int)sum);
            }
             * */
            int bass1_max = 80 / (wi.WaveFormat.SampleRate / fftLength);
            float sum = 0;
            float bassSum = 0;
            for (int i = 1; i <= bass1_max; i++)
            {
                sum += output[i];
            }
            bassSum = sum;
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(0, serialData, (int)sum, (int)0, (int)sum / 4);
            else
                LEDSetup.processColor(24, serialData, (int)sum, (int)0, (int)sum / 4);

            int bass2_max = 120 / (wi.WaveFormat.SampleRate / fftLength);
            sum = 0;
            for (int i = bass1_max - 2; i <= bass2_max; i++)
            {
                sum += output[i];
            }
            bassSum += sum;
            beatVals[beatVali++] = bassSum * bassSum;
            if (beatVali >= beatVals.Length) beatVali = 0;

#if DEBUG
            Console.WriteLine("{2}: Bass sum {0}, beat average {1}", bassSum, beatVals.Average(), DateTime.Now.Millisecond, scale);
            if (bassSum * bassSum > beatVals.Average() * 2 && bassSum > 200)
            {
                if (!beatHit)
                {
                    beats++;
                    Console.WriteLine("BEAT DETECTED!");
                }
                beatHit = true;
            }
            else beatHit = false;
#endif
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(1, serialData, (int)sum, (int)0, (int)sum / 4);
            else
                LEDSetup.processColor(23, serialData, (int)sum, (int)0, (int)sum / 4);

            int bass3_max = 170 / (wi.WaveFormat.SampleRate / fftLength);
            sum = 0;
            for (int i = bass2_max - 2; i <= bass3_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(2, serialData, (int)sum, (int)0, (int)sum / 2);
            else
                LEDSetup.processColor(22, serialData, (int)sum, (int)0, (int)sum / 2);

            int bass4_max = 220 / (wi.WaveFormat.SampleRate / fftLength);
            sum = 0;
            for (int i = bass3_max - 2; i <= bass4_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(3, serialData, (int)sum, (int)0, (int)sum / 2);
            else
                LEDSetup.processColor(21, serialData, (int)sum, (int)0, (int)sum / 2);

            int mid1_max = 500 / (wi.WaveFormat.SampleRate / fftLength); //440
            sum = 0;
            for (int i = bass4_max - 5; i <= mid1_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(4, serialData, (int)sum, (int)sum / 2, (int)sum / 2);
            else
                LEDSetup.processColor(20, serialData, (int)sum, (int)sum / 2, (int)sum / 2);

            int mid2_max = 900 / (wi.WaveFormat.SampleRate / fftLength);
            sum = 0;
            for (int i = mid1_max - 5; i <= mid2_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(5, serialData, (int)sum, (int)sum / 2, (int)sum / 2);
            else
                LEDSetup.processColor(19, serialData, (int)sum, (int)sum / 2, (int)sum / 2);

            int mid3_max = 2000 / (wi.WaveFormat.SampleRate / fftLength); //1200
            sum = 0;
            for (int i = mid2_max - 5; i <= mid3_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            LEDSetup.processColor(6, serialData, (int)sum, (int)sum / 2, (int)0);
            LEDSetup.processColor(18, serialData, (int)sum, (int)sum / 2, (int)0);

            int mid4_max = 4000 / (wi.WaveFormat.SampleRate / fftLength);
            sum = 0;
            for (int i = mid3_max - 5; i <= mid4_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(7, serialData, (int)sum, (int)sum / 2, (int)0);
            else
                LEDSetup.processColor(17, serialData, (int)sum, (int)sum / 2, (int)0);

            int mid5_max = 6000 / (wi.WaveFormat.SampleRate / fftLength); //5000
            sum = 0;
            for (int i = mid4_max - 5; i <= mid5_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(8, serialData, (int)sum, (int)sum / 2, (int)sum / 4);
            else
                LEDSetup.processColor(16, serialData, (int)sum, (int)sum / 2, (int)sum / 4);

            int mid6_max = 8000 / (wi.WaveFormat.SampleRate / fftLength); //5000
            sum = 0;
            for (int i = mid5_max - 5; i <= mid6_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(9, serialData, (int)sum, (int)sum / 2, (int)sum / 4);
            else
                LEDSetup.processColor(15, serialData, (int)sum, (int)sum / 2, (int)sum / 4);

            int treb1_max = 10000 / (wi.WaveFormat.SampleRate / fftLength); //8000
            sum = 0;
            for (int i = mid6_max - 10; i <= treb1_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(10, serialData, (int)sum, (int)sum, (int)sum / 2);
            else
                LEDSetup.processColor(14, serialData, (int)sum, (int)sum, (int)sum / 2);

            int treb2_max = 12000 / (wi.WaveFormat.SampleRate / fftLength); //8000
            sum = 0;
            for (int i = treb1_max - 10; i <= treb2_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            if (channel == 1)
                LEDSetup.processColor(11, serialData, (int)sum, (int)sum, (int)sum / 2);
            else
                LEDSetup.processColor(13, serialData, (int)sum, (int)sum, (int)sum / 2);

            int treb3_max = 15000 / (wi.WaveFormat.SampleRate / fftLength); //8000
            sum = 0;
            for (int i = treb2_max - 10; i <= treb3_max; i++)
            {
                sum += output[i];
            }
            sum /= 100;
            sum *= 4000 / scale;
            if (sum > 256) sum = 256;
            LEDSetup.processColor(12, serialData, (int)(sum + serialData[6 + 12 * 3]) / 2, (int)(sum + serialData[6 + 12 * 3 + 1]) / 2, (int)((sum * 0.75) + serialData[6 + 12 * 3 + 2]) / 2);

            fftSent |= channel + 1;
            if (fftSent == 3)
            {
                LEDSetup.sendSerialData(serialData);
                fftSent = 0;
            }
        }
    }

    public static class VolumeUtilities
    {
        public static float GetMasterVolume()
        {
            // get the speakers (1st render + multimedia) device
            IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
            IMMDevice speakers;
            const int eRender = 0;
            const int eMultimedia = 1;
            deviceEnumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out speakers);

            object o;
            speakers.Activate(typeof(IAudioEndpointVolume).GUID, 0, IntPtr.Zero, out o);
            IAudioEndpointVolume aepv = (IAudioEndpointVolume)o;
            float volume = aepv.GetMasterVolumeLevelScalar();
            Marshal.ReleaseComObject(aepv);
            Marshal.ReleaseComObject(speakers);
            Marshal.ReleaseComObject(deviceEnumerator);
            return volume;
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            void _VtblGap1_6();
            float GetMasterVolumeLevelScalar();
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            void _VtblGap1_1();

            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }
    }
}
