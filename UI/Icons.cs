using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcher2.UI
{
    public static class Icons
    {
        public const string Icon_Play = "play";
        public const string Icon_Stop = "stop";
        public const string Icon_Break = "break";
        public const string Icon_Step = "step";
        public const string Icon_Analyze = "analyze";

        public const string Icon_ActionAdd = "actionAdd";
        public const string Icon_ActionRemove = "actionRemove";

        public const string Icon_Back = "back";
        public const string Icon_Forward = "forward";
        public const string Icon_Refresh = "refresh";


        public const string Icon_EditCut = "cut";
        public const string Icon_EditCopy = "copy";
        public const string Icon_EditPaste = "paste";

        public const string Icon_FileOpen = "open";
        public const string Icon_FileOpenFolder = "openDir";
        public const string Icon_FileSave = "save";
        public const string Icon_FileSaveAll = "saveAll";

        public static System.Windows.Forms.ImageList ImageList { get; private set; }

        private static System.Drawing.Font CreateIconFont(int height) {
            return new System.Drawing.Font("wingdings", height, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        }

        public static System.Drawing.Image CreateWingdingsIcon(char charValue, System.Drawing.Brush brush = null, System.Drawing.Size? size = null, System.Drawing.Font font = null)
        {
            if (!size.HasValue) size = new System.Drawing.Size(System.Drawing.SystemFonts.MenuFont.Height, System.Drawing.SystemFonts.MenuFont.Height);
            if (brush == null) brush = System.Drawing.Brushes.DeepSkyBlue;
            var rect = new System.Drawing.Rectangle(0, 0, size.Value.Width, size.Value.Height);
            var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height);
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.Clear(ImageList.TransparentColor);
                    bool disposeFont = false;
                    try {

                        if (font == null) { font = CreateIconFont((int)(rect.Height * 0.85)); disposeFont = true; }
                        using (var stringFormat = new System.Drawing.StringFormat()
                        {
                            Alignment = System.Drawing.StringAlignment.Center,
                            LineAlignment = System.Drawing.StringAlignment.Center,
                            Trimming = System.Drawing.StringTrimming.None,
                            FormatFlags = System.Drawing.StringFormatFlags.NoFontFallback | System.Drawing.StringFormatFlags.LineLimit,
                        })
                            g.DrawString(charValue.ToString(), font, brush, rect, stringFormat);
                    } finally {
                        if (disposeFont && font != null) font.Dispose();
                    }

                }
            }
            return bmp;
        }

        public static int IndexOfIcon(string key) { return ImageList.Images.IndexOfKey(key); }
        public static System.Drawing.Image GetIcon(string key) { return ImageList.Images[key]; }

        private static void AddUnicodeIcon(string key, char charValue, System.Drawing.Brush brush = null, System.Drawing.Font font = null)
        {
            var icon = CreateWingdingsIcon(charValue, brush, ImageList.ImageSize, font);
            ImageList.Images.Add(key, icon);
        }

        static Icons()
        {
            ImageList = new System.Windows.Forms.ImageList() {
                ImageSize = new System.Drawing.Size(18, 18),
                TransparentColor = System.Drawing.Color.Magenta,
                ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit, 
            };

            using (var font = CreateIconFont(ImageList.ImageSize.Height)) {
                AddUnicodeIcon(Icon_Play, (char)0x6C);
                AddUnicodeIcon(Icon_Stop, (char)0x6E);
                AddUnicodeIcon(Icon_Break, (char)0xA2);
                AddUnicodeIcon(Icon_Step, (char)0xA4);
                AddUnicodeIcon(Icon_Analyze, (char)0xB7);

                AddUnicodeIcon(Icon_ActionAdd, (char)0x7C);
                AddUnicodeIcon(Icon_ActionRemove, (char)0xFB);

                AddUnicodeIcon(Icon_Back, (char)0xE7);
                AddUnicodeIcon(Icon_Forward, (char)0xE8);
                AddUnicodeIcon(Icon_Refresh, (char)0xEE);

                AddUnicodeIcon(Icon_EditCut, (char)0x23);
                AddUnicodeIcon(Icon_EditCopy, (char)0x32);
                AddUnicodeIcon(Icon_EditPaste, (char)0x34);

                AddUnicodeIcon(Icon_FileOpen, (char)0x30);
                AddUnicodeIcon(Icon_FileOpenFolder, (char)0x31);
                AddUnicodeIcon(Icon_FileSave, (char)0x3C);
                AddUnicodeIcon(Icon_FileSaveAll, (char)0x3D);
            }
        }
    }

    public class ToolbarButton : System.Windows.Forms.ToolStripButton
    {
        public ToolbarButton(string text, string imageKey = null, EventHandler onClick = null, string name = null) 
            : base(text, null, onClick, name)
        {
            DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            ImageAlign = System.Drawing.ContentAlignment.BottomLeft;
            ImageKey = imageKey;
            AutoSize = false;
            AutoToolTip = true;
        }
    }

    public class MenuButton : System.Windows.Forms.ToolStripMenuItem
    {
        public MenuButton(string text, string imageKey = null, EventHandler onClick = null, System.Windows.Forms.Keys shortcutKeys = System.Windows.Forms.Keys.None, string name = null)
            : base(text, null, onClick, shortcutKeys)
        {
            if (name != null) Name = name;
            ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            //ImageAlign = System.Drawing.ContentAlignment.BottomLeft;
            DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.ImageAndText;
            Image = Icons.GetIcon(imageKey);
        }
    }

}
