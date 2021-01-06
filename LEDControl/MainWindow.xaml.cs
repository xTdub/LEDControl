using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LEDControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public TrayUtil trayIcon;

        public MainWindow()
        {
            InitializeComponent();
        }

        private double scaleFactor = 1.0;
        private List<string> audioDeviceNames;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reload();

            var screen = System.Windows.Forms.Screen.AllScreens[0];
            scaleFactor = 200.0 / (double)screen.Bounds.Height;
            borderScreen.Width = screen.Bounds.Width * scaleFactor;
            //drawLEDs();
            //Glass10.EnableBlur(this);

            audioDeviceNames = new List<string>();
            for(int i=0; i< WaveIn.DeviceCount; i++)
            {
                audioDeviceNames.Add(WaveIn.GetCapabilities(i).ProductName);
            }
            comboBoxAudioDevices.ItemsSource = audioDeviceNames;
        }

        private void removeLEDs()
        {
            borderScreen.Background = Brushes.White;
            for (int i = gridScreenRep.Children.Count - 1; i >= 0; i--)
            {
                if ((gridScreenRep.Children[i] as Border).Name != "borderScreen") gridScreenRep.Children.Remove(gridScreenRep.Children[i] as UIElement);
            }
        }
        private void drawLEDs()
        {
            foreach (var led in LEDSetup.leds)
            {
                Border b = new Border();
                b.BorderBrush = Brushes.Black;
                b.BorderThickness = new Thickness(1);
                b.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                b.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                b.Height = led.Coords.Height * scaleFactor;
                b.Width = led.Coords.Width * scaleFactor;
                b.Margin = new Thickness(led.Coords.X * scaleFactor, led.Coords.Y * scaleFactor, 0, 0);
                gridScreenRep.Children.Add(b);
            }
        }

        private void drawLEDsColor()
        {
            var screenBitmap = ScreenColors.getScreenGDI(0);

            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memory = new MemoryStream())
            {
                screenBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }
            var bgBrush = new ImageBrush();
            bgBrush.ImageSource = bitmapImage;
            borderScreen.Background = bgBrush;

            foreach (var led in LEDSetup.leds)
            {
                int r = 0, g = 0, b = 0, c = 0;
                long re = 0, gr = 0, bl = 0;
                for (int x = (int)led.Coords.Left; x < (int)led.Coords.Right; x += 10)
                {
                    for (int y = (int)led.Coords.Top; y < (int)led.Coords.Bottom; y += 10)
                    {
                        System.Drawing.Color col;
                        lock (screenBitmap)
                        {
                            col = screenBitmap.GetPixel(x, y);
                        }
                        re += (long)(col.R * col.R);
                        gr += (long)(col.G * col.G);
                        bl += (long)(col.B * col.B);
                        c++;
                    }
                }
                r = (int)Math.Sqrt(re / c);
                g = (int)Math.Sqrt(gr / c);
                b = (int)Math.Sqrt(bl / c);
                if (r > 255) r = 255;
                if (g > 255) g = 255;
                if (b > 255) b = 255;

                Border br = new Border();
                br.BorderBrush = Brushes.Black;
                br.BorderThickness = new Thickness(1);
                br.Background = new SolidColorBrush(new Color() {A=255, R = (byte)r, G = (byte)g, B = (byte)b });
                br.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                br.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                br.Height = led.Coords.Height * scaleFactor;
                br.Width = led.Coords.Width * scaleFactor;
                br.Margin = new Thickness(led.Coords.X * scaleFactor, led.Coords.Y * scaleFactor, 0, 0);
                gridScreenRep.Children.Add(br);
            }

            //screenBitmap.Dispose();
        }

        private void Button_addLEDs(object sender, RoutedEventArgs e)
        {
            removeLEDs();
            drawLEDs();
        }

        private void Button_removeLEDs(object sender, RoutedEventArgs e)
        {
            removeLEDs();
        }

        private void Button_addColorLEDs(object sender, RoutedEventArgs e)
        {
            removeLEDs();
            drawLEDsColor();
        }

        private void upDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.OldValue == null) return;
            LEDSetup.pixelSize = (int)e.NewValue;
            LEDSetup.resetLEDs();
            removeLEDs();
            drawLEDs();
        }

        private void canvasColor_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (!canvasColor.SelectedColor.HasValue) return;
            Color col = canvasColor.SelectedColor.Value;
            byte[] packet = LEDSetup.getMagicHeader();
            for (int i = 0; i < LEDSetup.LED_C; i++)
            {
                LEDSetup.processColorSimple(i, packet, col.R, col.G, col.B);
            }
            LEDSetup.sendSerialData(packet);
        }

        private void canvasColor_MouseEnter(object sender, MouseEventArgs e)
        {
            LEDSetup.OVERRIDE = true;
        }

        private void canvasColor_MouseLeave(object sender, MouseEventArgs e)
        {
            LEDSetup.OVERRIDE = false;
        }

        private void comboBoxAudioDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AudioColors.DeviceName = comboBoxAudioDevices.SelectedItem as string;
        }
    }
}
