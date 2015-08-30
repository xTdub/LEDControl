using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.ApplicationServices;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;

namespace LEDControl
{
    /// <summary>
    /// Interaction logic for TrayUtil.xaml
    /// </summary>
    public partial class TrayUtil : Window
    {
        public static readonly RoutedUICommand Exit = new RoutedUICommand("Exit", "Exit", typeof(TrayUtil));
        private CommandBinding Exit_Binding = new CommandBinding(Exit, Exit_Handler);

        public TrayUtil()
        {
            InitializeComponent();

            this.CommandBindings.Add(Exit_Binding);

            TrayIcon.ToolTipText = "LED Backlight Control";
            TrayIcon.ContextMenu = new ContextMenu();
            TrayIcon.ContextMenu.ItemsSource = new List<MenuItem>() { new MenuItem() { Header = "E_xit", Command = Exit, CommandTarget=TrayIcon } };
            Logger.Cleanup(10);
            Logger.QueueLine("Completed initial startup");

            PowerManager.IsMonitorOnChanged += PowerManager_IsMonitorOnChanged;

            initLEDs();
            Heartbeat = new System.Timers.Timer(1000);
            Heartbeat.Elapsed += Heartbeat_Elapsed;
            Heartbeat.Start();
        }
        DateTime lastBeat = DateTime.Now;
        void Heartbeat_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBlockFPS.Text = "FPS: " + LEDSetup.frameCount;
                LEDSetup.frameCount = 0;
                if ((DateTime.Now - lastBeat).TotalSeconds > 5)
                {
                    lastBeat = DateTime.Now;
                    if (aColors != null)
                    {
                        textBlockBeats.Text = "BPM: " + 6 * aColors.beats;
                        aColors.beats = aColors.beats / aColors.fftCount;
                    }
                }
            }));
            if (LEDSetup.ActivePort == null)
            {
                Logger.QueueLine("No active port, checking for controllers...");
                if (LEDSetup.Setup() == null)
                {
                    Logger.QueueLine("No active Adalight controller detected");
                }
                else
                {
                    Logger.QueueLine("Starting LED draw timer");
                    DrawTimer = new System.Timers.Timer(1000.0 / 30.0);
                    DrawTimer.Elapsed += DrawTimer_Elapsed;
                    DrawTimer.Start();
                }
            }
            else if (!LEDSetup.ActivePort.IsOpen)
            {
                Logger.QueueLine("Active port closed, checking for controllers...");
                if (LEDSetup.Setup() == null)
                {
                    Logger.QueueLine("No active Adalight controller detected");
                }
                else
                {
                    Logger.QueueLine("Starting LED draw timer");
                    DrawTimer = new System.Timers.Timer(1000.0 / 30.0);
                    DrawTimer.Elapsed += DrawTimer_Elapsed;
                    DrawTimer.Start();
                }
            }
        }

        private static LEDMode CurrentMode = LEDMode.Screen;

        void PowerManager_IsMonitorOnChanged(object sender, EventArgs e)
        {
            if (PowerManager.IsMonitorOn)
            {
                Logger.QueueLine("Monitor has turned on, resuming LEDs");
                LEDSetup.LEDS_ON = true;
                if (LEDSetup.ActivePort != null)
                {
                    if(sColors != null)
                        DrawTimer.Start();
                    if (aColors != null)
                        aColors.wi.StartRecording();
                }
            }
            else
            {
                Logger.QueueLine("Monitor has turned off, disabling LEDs");
                LEDSetup.LEDS_ON = false;
                if (LEDSetup.ActivePort != null)
                {
                    if (sColors != null)
                        DrawTimer.Stop();
                    if (aColors != null)
                        aColors.wi.StopRecording();
                    System.Threading.Thread.Sleep(50);
                    LEDSetup.drawOff();
                }
            }
        }

        public static double brightnessModifier = 1.0;
        private void sliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            brightnessModifier = sliderBrightness.Value;
            textBlockBrightness.Text = String.Format("{0:F0}%", brightnessModifier*100);
        }
        private void sliderBrightness_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) sliderBrightness.Value += 0.02;
            else sliderBrightness.Value -= 0.02;
        }
        private void sliderFade_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LEDSetup.fade = (short)sliderFade.Value;
            textBlockFade.Text = String.Format("{0}", LEDSetup.fade);
        }
        private void sliderFade_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) sliderFade.Value += 2;
            else sliderFade.Value -= 2;
        }
        private static System.Timers.Timer DrawTimer;
        private static System.Timers.Timer Heartbeat;

        static ScreenColors sColors;
        static AudioColors aColors;

        private void initLEDs()
        {
            sColors = new ScreenColors();
            //aColors = new AudioColors();
            if (LEDSetup.Setup() == null)
            {
                Logger.QueueLine("No active Adalight controller detected");
                Dispatcher.BeginInvoke(new Action(() => { MessageBox.Show("No active Adalight controller detected"); }));
                
            }
            else
            {
                Logger.QueueLine("Starting LED draw timer");
                DrawTimer = new System.Timers.Timer(1000.0 / 30.0);
                DrawTimer.Elapsed += DrawTimer_Elapsed;
                DrawTimer.Start();
            }
        }

        void DrawTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Dispatcher.BeginInvoke(new Action(drawRainbow));
            if (DrawTimer.Enabled)
            {
                if (sColors.device == null) sColors.setupDX();
                else Task.Run(new Action(sColors.draw));
            }
            //drawSolid();
            //draw();
        }
        /*
        private void drawRainbow()
        {
            byte[] serialData = getMagicHeader();
            int j = 6;
            //Color[] colors = new Color[7] { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Indigo, Color.Violet };
            System.Drawing.Color[] colors = new System.Drawing.Color[7];
            for (int i = 0; i < LED_C; i++)
            {
                ledColor[i, 0] = (byte)colors[i % 7].R;
                ledColor[i, 1] = (byte)colors[i % 7].G;
                ledColor[i, 2] = (byte)colors[i % 7].B;

                if (DateTime.Now.Second % 2 == 0)
                {
                    serialData[j++] = gamma[ledColor[i, 0], 0];
                    serialData[j++] = gamma[ledColor[i, 1], 1];
                    serialData[j++] = gamma[ledColor[i, 2], 2];
                }
                else
                {
                    serialData[j++] = ledColor[i, 0];
                    serialData[j++] = ledColor[i, 1];
                    serialData[j++] = ledColor[i, 2];
                }
            }
            if (ActivePort != null) ActivePort.Write(serialData, 0, serialData.Length);

            //ledColor.CopyTo(prevColor, 0);
            Array.Copy(ledColor, 0, prevColor, 0, ledColor.Length);
        }

        private void drawSolid()
        {
            byte[] serialData = LEDSetup.getMagicHeader();
            int j = 6;
            for (int i = 0; i < LED_C; i++)
            {
                ledColor[i, 0] = (byte)(255 * sliderBrightness.Value);
                ledColor[i, 1] = (byte)(255 * sliderBrightness.Value);
                ledColor[i, 2] = (byte)(255 * sliderBrightness.Value);
                // Apply gamma curve and place in serial output buffer
                serialData[j++] = gamma[ledColor[i, 0], 0];
                serialData[j++] = gamma[ledColor[i, 1], 1];
                serialData[j++] = gamma[ledColor[i, 2], 2];
            }
            if (ActivePort != null) ActivePort.Write(serialData, 0, serialData.Length);

            //ledColor.CopyTo(prevColor, 0);
            Array.Copy(ledColor, 0, prevColor, 0, ledColor.Length);
        }
        */
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            DrawTimer.Stop();
            System.Threading.Thread.Sleep(50);
            if(LEDSetup.ActivePort != null)
                if (LEDSetup.ActivePort.IsOpen) LEDSetup.drawOff();
            TrayIcon.Dispose();
            Logger.QueueLine("Application closing");
            base.OnClosing(e);
        }

        private static void Exit_Handler(object sender, ExecutedRoutedEventArgs e)
        {
            Application.Current.Shutdown(0);
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            MainWindow controlPanel = new MainWindow();
            controlPanel.Show();
        }

        private void buttonSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentMode == LEDMode.Screen)
            {
                Logger.QueueLine("Switching LED Mode: Audio");
                CurrentMode = LEDMode.Audio;
                DrawTimer.Stop();
                aColors = new AudioColors();
                sColors.Dispose();
                sColors = null;
                buttonSwitch.Content = "_Switch to Screen";
            }
            else if (CurrentMode == LEDMode.Audio)
            {
                Logger.QueueLine("Switching LED Mode: Screen");
                CurrentMode = LEDMode.Screen;
                aColors.wi.StopRecording();
                sColors = new ScreenColors();
                aColors = null;
                DrawTimer.Start();
                buttonSwitch.Content = "_Switch to Audio";
            }
        }

    }
}
