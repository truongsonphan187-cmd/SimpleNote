using System;
using System.Windows;

namespace SimpleNote
{
    public partial class App : System.Windows.Application
    {
        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            try
            {
                if (MainWindow is MainWindow mw)
                {
                    mw.SaveNotesNow();
                }
            }
            catch { }

            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (MainWindow is MainWindow mw)
                {
                    mw.SaveNotesNow();
                }
            }
            catch { }

            base.OnExit(e);
        }
    }
}
