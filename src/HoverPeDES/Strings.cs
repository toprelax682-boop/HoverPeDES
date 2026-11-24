using System.Collections.Generic;
using System.Globalization;

namespace HoverPeDES;

/// <summary>Tiny English / Vietnamese localisation table.</summary>
internal static class L
{
    public static string Lang
    {
        get
        {
            var s = Settings.Current.Language;
            if (s == "en" || s == "vi") return s;
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vi" : "en";
        }
    }

    public static string T(string key)
    {
        var table = Lang == "vi" ? Vi : En;
        if (table.TryGetValue(key, out var v)) return v;
        return En.TryGetValue(key, out var e) ? e : key;
    }

    private static readonly Dictionary<string, string> En = new()
    {
        ["tip"] = "HoverPeDES - hover to preview",
        ["enable"] = "Enable hover preview",
        ["settings"] = "Settings...",
        ["extension"] = "Install browser extension...",
        ["log"] = "Open log file",
        ["about"] = "About",
        ["exit"] = "Exit",
        ["settingsTitle"] = "HoverPeDES settings",
        ["language"] = "Language",
        ["langAuto"] = "Automatic",
        ["delay"] = "Hover delay (ms)",
        ["width"] = "Preview width (px)",
        ["start"] = "Start playing at (% of video)",
        ["volume"] = "Volume (when not muted)",
        ["mute"] = "Mute preview (untick to hear sound)",
        ["images"] = "Also preview images in folders",
        ["audio"] = "Also preview audio files (with visualizer)",
        ["text"] = "Also preview text and code files",
        ["extra"] = "Extra file types (e.g. .xyz, .abc)",
        ["docs"] = "Also preview PDF, SVG, Word, Excel, PowerPoint",
        ["raw"] = "Also preview camera RAW photos",
        ["noText"] = "(No readable text in this document)",
        ["cantPreview"] = "Cannot preview this file",
        ["noWebView"] = "Showing PDF and SVG needs the free Microsoft Edge WebView2 Runtime.\n\nInstall it from:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703\n\nThen hover the file again.",
        ["ext"] = "Allow browser extension to use HoverPeDES",
        ["autostart"] = "Start with Windows",
        ["ok"] = "Save",
        ["cancel"] = "Cancel",
        ["cantPlay"] = "Cannot play this file",
        ["loading"] = "Loading...",
        ["noMpv"] = "libmpv-2.dll was not found next to HoverPeDES.exe.\nPlease reinstall HoverPeDES.",
        ["portBusy"] = "Port 47653 is busy, so the browser extension cannot connect.",
        ["extTitle"] = "Install the browser extension",
        ["extBody"] = "The extension folder is now open and its path is copied to the clipboard.\n\n1. In Chrome / Edge, open the Extensions page (opened for you).\n2. Turn on \"Developer mode\".\n3. Click \"Load unpacked\" and choose the folder:\n\n{0}\n\nThe extension then works on every website.",
        ["aboutBody"] = "HoverPeDES {0}\nHover a video or image to preview it.\nPlayback engine: mpv. Web video: yt-dlp.\n\nHotkeys while previewing:\nCtrl+Alt+Space  pause / play\nCtrl+Alt+Left / Right  seek 5 s\nCtrl+Alt+M  mute / unmute",
        ["started"] = "HoverPeDES is running in the tray. Hover a video in a folder to preview it. Right-click the icon to install the browser extension.",
    };

    private static readonly Dictionary<string, string> Vi = new()
    {
        ["tip"] = "HoverPeDES - rê chuột để xem trước",
        ["enable"] = "Bật xem trước khi rê chuột",
        ["settings"] = "Cài đặt...",
        ["extension"] = "Cài tiện ích trình duyệt...",
        ["log"] = "Mở file nhật ký",
        ["about"] = "Giới thiệu",
        ["exit"] = "Thoát",
        ["settingsTitle"] = "Cài đặt HoverPeDES",
        ["language"] = "Ngôn ngữ",
        ["langAuto"] = "Tự động",
        ["delay"] = "Độ trễ khi rê chuột (ms)",
        ["width"] = "Chiều rộng khung xem trước (px)",
        ["start"] = "Bắt đầu phát tại (% video)",
        ["volume"] = "Âm lượng (khi không tắt tiếng)",
        ["mute"] = "Tắt tiếng khi xem trước (bỏ chọn để nghe âm thanh)",
        ["images"] = "Xem trước cả ảnh trong thư mục",
        ["audio"] = "Xem trước cả file âm thanh (có hiệu ứng sóng nhạc)",
        ["text"] = "Xem trước cả file văn bản và mã nguồn",
        ["extra"] = "Định dạng bổ sung (vd: .xyz, .abc)",
        ["docs"] = "Xem trước cả PDF, SVG, Word, Excel, PowerPoint",
        ["raw"] = "Xem trước cả ảnh RAW của máy ảnh",
        ["noText"] = "(Tài liệu không có văn bản đọc được)",
        ["cantPreview"] = "Không xem trước được tệp này",
        ["noWebView"] = "Xem PDF và SVG cần Microsoft Edge WebView2 Runtime (miễn phí).\n\nCài tại:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703\n\nSau đó rê chuột vào file lại.",
        ["ext"] = "Cho phép tiện ích trình duyệt dùng HoverPeDES",
        ["autostart"] = "Khởi động cùng Windows",
        ["ok"] = "Lưu",
        ["cancel"] = "Hủy",
        ["cantPlay"] = "Không phát được tệp này",
        ["loading"] = "Đang tải...",
        ["noMpv"] = "Không tìm thấy libmpv-2.dll cạnh HoverPeDES.exe.\nHãy cài đặt lại HoverPeDES.",
        ["portBusy"] = "Cổng 47653 đang bận nên tiện ích trình duyệt không kết nối được.",
        ["extTitle"] = "Cài tiện ích trình duyệt",
        ["extBody"] = "Thư mục tiện ích đã được mở và đường dẫn đã được sao chép vào clipboard.\n\n1. Trong Chrome / Edge, mở trang Tiện ích (đã mở sẵn cho bạn).\n2. Bật \"Chế độ nhà phát triển\".\n3. Bấm \"Tải tiện ích đã giải nén\" rồi chọn thư mục:\n\n{0}\n\nSau đó tiện ích hoạt động trên mọi trang web.",
        ["aboutBody"] = "HoverPeDES {0}\nRê chuột vào video hoặc ảnh để xem trước.\nBộ phát: mpv. Video trên web: yt-dlp.\n\nPhím tắt khi đang xem trước:\nCtrl+Alt+Space  tạm dừng / phát\nCtrl+Alt+Trái / Phải  tua 5 giây\nCtrl+Alt+M  bật / tắt tiếng",
        ["started"] = "HoverPeDES đang chạy ở khay hệ thống. Rê chuột vào video trong thư mục để xem trước. Bấm chuột phải vào biểu tượng để cài tiện ích trình duyệt.",
    };
}
