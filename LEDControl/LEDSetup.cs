using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LEDControl
{
    class LEDSetup
    {
        public const int BAUDRATE = 115200;
        public const int GRID_W = 9;
        public const int GRID_H = 6;
        public const int LED_C = 25;
        public const int SCREEN_W = 1920;
        public const int SCREEN_H = 1080;

        public static short minBrightness = 120;
        public static short fade = 64;
        public static int pixelSize = 120;

        public static bool LEDS_ON = true;

        public static SerialPort ActivePort;
        public static byte[,] gamma = new byte[256, 3];
        public static byte[,] ledColor = new byte[LED_C, 3];
        public static byte[,] prevColor = new byte[LED_C, 3];
        public static LEDInfo[] leds = new LEDInfo[LED_C] { //start at bottom right, go anticlockwise
            //Bottom right
            new LEDInfo(5,5),
            new LEDInfo(6,5),
            new LEDInfo(7,5),
            new LEDInfo(8,5),
            //right
            new LEDInfo(8,4),
            new LEDInfo(8,3),
            new LEDInfo(8,2),
            new LEDInfo(8,1),
            //top
            new LEDInfo(8,0),
            new LEDInfo(7,0),
            new LEDInfo(6,0),
            new LEDInfo(5,0),
            new LEDInfo(4,0),
            new LEDInfo(3,0),
            new LEDInfo(2,0),
            new LEDInfo(1,0),
            new LEDInfo(0,0),
            //left
            new LEDInfo(0,1),
            new LEDInfo(0,2),
            new LEDInfo(0,3),
            new LEDInfo(0,4),
            //bottom left
            new LEDInfo(0,5),
            new LEDInfo(1,5),
            new LEDInfo(2,5),
            new LEDInfo(3,5)
        };

        public static int frameCount = 0;

        public static void resetLEDs()
        {
            leds = new LEDInfo[LED_C] { //start at bottom right, go anticlockwise
                //Bottom right
                new LEDInfo(5,5),
                new LEDInfo(6,5),
                new LEDInfo(7,5),
                new LEDInfo(8,5),
                //right
                new LEDInfo(8,4),
                new LEDInfo(8,3),
                new LEDInfo(8,2),
                new LEDInfo(8,1),
                //top
                new LEDInfo(8,0),
                new LEDInfo(7,0),
                new LEDInfo(6,0),
                new LEDInfo(5,0),
                new LEDInfo(4,0),
                new LEDInfo(3,0),
                new LEDInfo(2,0),
                new LEDInfo(1,0),
                new LEDInfo(0,0),
                //left
                new LEDInfo(0,1),
                new LEDInfo(0,2),
                new LEDInfo(0,3),
                new LEDInfo(0,4),
                //bottom left
                new LEDInfo(0,5),
                new LEDInfo(1,5),
                new LEDInfo(2,5),
                new LEDInfo(3,5)
            };
        }

        public static SerialPort Setup()
        {
            //Initialize prevcolor array for fade
            for (int i = 0; i < LED_C; i++)
            {
                prevColor[i, 0] = prevColor[i, 1] = prevColor[i, 2] =
                  (byte)(minBrightness / 3);
            }

            // Pre-compute gamma correction table for LED brightness levels:
            for (int i = 0; i < 256; i++)
            {
                double f = Math.Pow((double)i / 255.0, 2.8);
                gamma[i, 0] = (byte)(f * 255.0);
                //gamma[i, 1] = (byte)(f * 240.0);
                gamma[i, 1] = (byte)(f * 220.0);
                //gamma[i, 2] = (byte)(f * 220.0);
                gamma[i, 2] = (byte)(f * 180.0);
            }

            ActivePort = null;
            string[] ports = SerialPort.GetPortNames();
            if (ports.Count() > 0)
            {
                Logger.QueueLine("Ports found: {0}\r\n{1}", ports.Count(), string.Join("\r\n", ports));
                foreach (var port in ports)
                {
                    Logger.QueueLine("Scanning port \"{0}\" with baud {1}", port, BAUDRATE);
                    var p = new SerialPort(port, BAUDRATE);
                    try
                    {
                        p.Open();
                    }
                    catch (Exception e)
                    {
                        Logger.QueueException("Failed to open port", e);
                        continue;
                    }
                    //p.Write(getMagicHeader(), 0, 6);
                    p.ReadTimeout = 2000;
                    try
                    {
                        var str = p.ReadLine();
                        //byte[] data = new byte[4];
                        //await p.BaseStream.ReadAsync(data, 0, 4);
                        //var str = ASCIIEncoding.ASCII.GetString(data);
                        if (str == "Ada")
                        {
                            Logger.QueueLine("Adalight controller found on port \"{0}\"", port);
                            ActivePort = p;
                            break;
                        }
                        else
                        {
                            Logger.QueueLine("Controller not found: Incorrect data");
                            p.Close();
                            p.Dispose();
                        }
                    }
                    catch (TimeoutException e)
                    {
                        Logger.QueueLine("Controller not found: Port timed out");
                        p.Close();
                        p.Dispose();
                    }
                }
            }
            else
            {
                Logger.QueueLine("No available serial ports");
            }
            return ActivePort;
        }

        public static byte[] getMagicHeader()
        {
            byte[] header = new byte[6 + LED_C * 3];
            header[0] = (byte)'A';
            header[1] = (byte)'d';
            header[2] = (byte)'a';
            header[3] = (byte)((LED_C - 1) >> 8);
            header[4] = (byte)((LED_C - 1) & 0xFF);
            header[5] = (byte)(header[3] ^ header[4] ^ 0x55);
            return header;
        }

        public static void processColor(int led, byte[] serialData, int r, int g, int b)
        {
            short weight = (short)(257 - fade); // 'Weighting factor' for new frame vs. old

            //Apply brightness modifier
            r = (int)(r * TrayUtil.brightnessModifier);
            if (r > 255) r = 255;
            g = (int)(g * TrayUtil.brightnessModifier);
            if (g > 255) g = 255;
            b = (int)(b * TrayUtil.brightnessModifier);
            if (b > 255) b = 255;

            // Blend new pixel value with the value from the prior frame
            ledColor[led, 0] = (byte)((((r) & 0xff) * weight +
                                       prevColor[led, 0] * fade) >> 8);
            ledColor[led, 1] = (byte)((((g) & 0xff) * weight +
                                       prevColor[led, 1] * fade) >> 8);
            ledColor[led, 2] = (byte)((((b) & 0xff) * weight +
                                       prevColor[led, 2] * fade) >> 8);

            // Boost pixels that fall below the minimum brightness
            int sum = ledColor[led, 0] + ledColor[led, 1] + ledColor[led, 2];
            if (sum < minBrightness)
            {
                if (sum == 0)
                { // To avoid divide-by-zero
                    byte deficit = (byte)(minBrightness / 3); // Spread equally to R,G,B
                    ledColor[led, 0] += deficit;
                    ledColor[led, 1] += deficit;
                    ledColor[led, 2] += deficit;
                }
                else
                {
                    int deficit = minBrightness - sum;
                    int s2 = sum * 2;
                    // Spread the "brightness deficit" back into R,G,B in proportion to
                    // their individual contribition to that deficit.  Rather than simply
                    // boosting all pixels at the low end, this allows deep (but saturated)
                    // colors to stay saturated...they don't "pink out."
                    ledColor[led, 0] += (byte)(deficit * (sum - ledColor[led, 0]) / s2);
                    ledColor[led, 1] += (byte)(deficit * (sum - ledColor[led, 1]) / s2);
                    ledColor[led, 2] += (byte)(deficit * (sum - ledColor[led, 2]) / s2);
                }
            }

            // Apply gamma curve and place in serial output buffer
            int j = (led * 3) + 6;
            serialData[j] = gamma[ledColor[led, 0], 0];
            serialData[j+1] = gamma[ledColor[led, 1], 1];
            serialData[j+2] = gamma[ledColor[led, 2], 2];
        }

        public static void sendSerialData(byte[] serialData)
        {
            if (ActivePort != null) ActivePort.Write(serialData, 0, serialData.Length);
            Array.Copy(ledColor, 0, prevColor, 0, ledColor.Length);
            frameCount++;
        }

        public static void drawOff()
        {
            Logger.QueueLine("Sending off command to LEDs");
            byte[] serialData = getMagicHeader();
            int j = 6;
            for (int i = 0; i < LED_C; i++)
            {
                serialData[j++] = 0;
                serialData[j++] = 0;
                serialData[j++] = 0;
            }
            if (ActivePort != null) ActivePort.Write(serialData, 0, serialData.Length);
        }
    }
    public class LEDInfo
    {
        public int X, Y, Screen;
        public Rect Coords;
        /*
        public LEDInfo(int x, int y, int screen = 1)
        {
            X = x;
            Y = y;
            Screen = screen;
            var bounds = System.Windows.Forms.Screen.AllScreens[screen].Bounds;
            var left = (x + 0.5) * (bounds.Width / LEDSetup.GRID_W) - (LEDSetup.pixelSize / 2);
            var top = (y + 0.5) * (bounds.Height / LEDSetup.GRID_H) - (LEDSetup.pixelSize / 2);
            Coords.Width = Coords.Height = LEDSetup.pixelSize;
            if (x == 0)
            {
                Coords.Width += LEDSetup.pixelSize / 2;
            }
            if (x == LEDSetup.GRID_W - 1)
            {
                Coords.Width += LEDSetup.pixelSize / 2;
                left -= LEDSetup.pixelSize / 2;
            }
            if (y == 0)
            {
                Coords.Height += LEDSetup.pixelSize / 2;
            }
            if (y == LEDSetup.GRID_H - 1)
            {
                Coords.Height += LEDSetup.pixelSize / 2;
                top -= LEDSetup.pixelSize / 2;
            }
            Coords.Location = new System.Windows.Point(left, top);
        }*/
        public LEDInfo(int x, int y, int screen = 1)
        {
            X = x;
            Y = y;
            Screen = screen;
            var bounds = System.Windows.Forms.Screen.AllScreens[screen].Bounds;
            int dx = bounds.Width / LEDSetup.GRID_W;
            var oddx = bounds.Width - (dx * LEDSetup.GRID_W);
            int dy = bounds.Height / LEDSetup.GRID_H;
            int left = x <= 4 ? dx * x : dx * x + oddx;
            int top = y * dy;
            Coords.Height = dy;
            Coords.Width = x == 4 ? dx + oddx : dx;
            Coords.Location = new Point(left, top);
        }
    }
    public enum LEDMode
    {
        Screen,
        Audio
    }
}
