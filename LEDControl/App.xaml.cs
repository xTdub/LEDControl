using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LEDControl
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //App.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            Logger.Init();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.QueueLine("Application is terminating");
            //Task.WaitAll(Logger.FlushQueueAsync());
            base.OnExit(e);
        }

        void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.QueueException("Dispatcher Unhandled Exception", e.Exception);
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                e.Handled = true;
                MessageBox.Show(e.Exception.Message, "Dispatcher Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //Task.WaitAll(Logger.FlushQueueAsync());
        }
        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Logger.QueueException("Domain Unhandled Exception", ex);
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                MessageBox.Show(ex.Message, "Domain Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //Task.WaitAll(Logger.FlushQueueAsync());
        }
    }
}
