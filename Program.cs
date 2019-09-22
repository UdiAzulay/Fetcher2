using System;
using System.Linq;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
namespace Fetcher2
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Cef.EnableHighDPISupport();
            CefSharpSettings.ShutdownOnExit = false;
            CefSharpSettings.SubprocessExitIfParentProcessClosed = false;
            CefSettings settings = new CefSettings() { };
            Cef.Initialize(settings);
            Application.Run(new UI.AppWindow(args.FirstOrDefault()));
            Cef.Shutdown();
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            var document = (e.Exception as UI.DocumentException)?.Document;
            UI.FileIO.MessageException(document, e.Exception);
        }
    }
}
