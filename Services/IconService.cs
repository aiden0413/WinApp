using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace winapp
{
    public static class IconService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static IconService()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.Timeout = TimeSpan.FromSeconds(3);
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteGdiObject(IntPtr hObject);

        // 1. 로컬 앱/파일 아이콘 가져오기
        public static BitmapSource? GetFileIcon(string filePath)
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                IntPtr hResult = SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

                if (shfi.hIcon != IntPtr.Zero)
                {
                    using (Icon icon = Icon.FromHandle(shfi.hIcon))
                    {
                        using (Bitmap bmp = icon.ToBitmap())
                        {
                            IntPtr hBitmap = bmp.GetHbitmap();
                            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            
                            DeleteGdiObject(hBitmap);
                            DestroyIcon(shfi.hIcon);
                            return bitmapSource;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // 2. 웹사이트 파비콘 및 타이틀 가져오기
        public static async Task<(BitmapSource? icon, string title)> GetWebSiteInfoAsync(string url)
        {
            BitmapSource? icon = null;
            string title = "";

            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                var uri = new Uri(url);

                string faviconUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64";
                var iconBytes = await _httpClient.GetByteArrayAsync(faviconUrl);
                if (iconBytes != null && iconBytes.Length > 0)
                {
                    using (var ms = new MemoryStream(iconBytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        icon = bitmap;
                    }
                }

                // 선언되어 있는 _httpClient를 사용하여 HTML 가져오기
                string html = await _httpClient.GetStringAsync(uri);
                var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                {
                    title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                }
                else
                {
                    title = string.Empty; // 매칭 실패 시 확실하게 비우기
                }

                // 타이틀을 못 가져왔거나 엉뚱한 값(gmail 등)이 들어온 경우 도메인으로 강제 대체
                if (string.IsNullOrWhiteSpace(title) || title.Equals("gmail", StringComparison.OrdinalIgnoreCase))
                {
                    title = uri.Host; // new Uri()로 감싸지 말고 바로 .Host 사용
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(title))
            {
                try { title = new Uri(url).Host; }
                catch { title = url; }
            }

            return (icon ?? GetDefaultGlobeIcon(), title);
        }

        public static BitmapSource? GetDefaultGlobeIcon()
        {
            try
            {
                var width = 32;
                var height = 32;
                var stride = width * 4;
                var pixelData = new byte[stride * height];
                return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixelData, stride);
            }
            catch
            {
                return null;
            }
        }
    }
}