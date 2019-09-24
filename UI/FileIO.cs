using System;
using System.Windows.Forms;

namespace Fetcher2.UI
{
    public static class FileIO
    {
        public static void MessageException(this IWin32Window owner, Exception ex)
        {
            MessageBox.Show(owner, ex.Message, "Error");
        }

        public static string GetFileName(this FileDialog fileDialog, IWin32Window owner, string fileName = null, string filter = null)
        {
            fileDialog.Filter = filter ?? "All files (*.*)|*.*";
            fileDialog.AddExtension = true;
            if (!string.IsNullOrEmpty(fileName))
            {
                fileDialog.DefaultExt = System.IO.Path.GetExtension(fileName);
                fileDialog.FileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            }
            if (fileDialog.ShowDialog(owner) != DialogResult.OK) return null;
            return fileDialog.FileName;
        }

        public static string GetOpenFileName(this IWin32Window owner, string fileName = null, string filter = null)
        {
            using (var dlgFileOpen = new OpenFileDialog() { CheckFileExists = true })
                return GetFileName(dlgFileOpen, owner, fileName, filter);
        }

        public static string GetSaveFileName(this IWin32Window owner, string fileName = null, string filter = null)
        {
            using (var dlgFileOpen = new SaveFileDialog())
            {
                var ret = GetFileName(dlgFileOpen, owner, fileName, filter);
                if (System.IO.File.Exists(ret)) System.IO.File.Delete(ret);
                return ret;
            }
        }
    }
}
