# HoverPeDES

**Tiếng Việt** · [English](#english)

Rê chuột vào video hoặc ảnh, khung xem trước tự bật ra và phát ngay. Hoạt động ở hai nơi:

| Nơi | Cách làm | Cần gì |
|---|---|---|
| **Thư mục / Desktop trên máy** (Explorer) | Rê chuột vào file video hoặc ảnh, khung nổi phát bằng **mpv** (hàng trăm định dạng: video mp4, mkv, avi, mov, flv, wmv, webm, ts, rmvb, vob...; âm thanh mp3, flac, wav, m4a, ogg, opus, wma, ape... có hiệu ứng sóng nhạc; ảnh jpg, png, gif, webp, avif, heic, tiff, ico...; văn bản và mã nguồn txt, md, json, csv, log, py, js, srt...; tài liệu PDF, SVG, Word, Excel, PowerPoint; ảnh RAW CR2/NEF/ARW/DNG và PSD) | Ứng dụng HoverPeDES |
| **Trang web** | Rê chuột vào ảnh: phóng to ngay trong trang. Rê vào video hoặc link video: gửi cho ứng dụng phát (link trực tiếp .mp4/.webm/.m3u8, YouTube, TikTok, Vimeo, Dailymotion, Bilibili, Twitch clip, Facebook/Instagram reel...) | Ứng dụng + tiện ích Chrome/Edge |

## Cài đặt (người dùng)

1. Tải `HoverPeDES-Setup-x.y.z.exe` ở trang **Releases**, chạy và cài (không cần quyền admin, có tiếng Việt).
2. Mở HoverPeDES (biểu tượng nằm ở khay hệ thống, cạnh đồng hồ).
3. Muốn dùng trên web: bấm chuột phải vào biểu tượng → **Cài tiện ích trình duyệt...** và làm theo hướng dẫn (Chrome/Edge → Extensions → bật Developer mode → Load unpacked → chọn thư mục `extension`).
   Hoặc cài từ cửa hàng tiện ích khi bản công khai đã được đăng.

Windows SmartScreen có thể cảnh báo "unknown publisher" vì file chưa được ký số. Bấm **More info → Run anyway**. Muốn hết cảnh báo cần mua chứng chỉ ký mã (hoặc dùng SignPath miễn phí cho dự án mã nguồn mở).

## Phím tắt khi đang xem trước

| Phím | Tác dụng |
|---|---|
| `Ctrl+Alt+Space` | Tạm dừng / phát |
| `Ctrl+Alt+←` / `→` | Tua 5 giây |
| `Ctrl+Alt+M` | Bật / tắt tiếng |

Video và âm thanh phát **có tiếng** theo mặc định (tick **Tắt tiếng** trong Cài đặt nếu không muốn). Với PDF, rê chuột sang khung xem trước rồi lăn chuột để cuộn trang. Bấm chuột (mở file) sẽ tự đóng khung xem trước. Cài đặt (độ trễ, kích thước, tắt tiếng, phát từ % nào, ngôn ngữ, khởi động cùng Windows) nằm trong menu khay hệ thống → **Cài đặt...**.

## Giới hạn cần biết

- **Video DRM** (Netflix, Disney+, ...) không xem trước được.
- **PDF và SVG** dùng Microsoft Edge WebView2 Runtime (có sẵn trên Windows 11 và Windows 10 đã cập nhật; nếu thiếu, app báo kèm link cài).
- **Word, Excel, PowerPoint** (docx, xlsx, pptx, odt, ods, odp) chỉ hiện **văn bản** trích từ file, không có định dạng và hình ảnh. File cũ .doc, .xls, .ppt chưa hỗ trợ.
- **Ảnh RAW** (CR2, NEF, ARW, DNG, ORF, RW2, RAF...) hiện ảnh JPEG xem trước nhúng sẵn trong file, không phải ảnh RAW đã xử lý. **PSD** và **PSB** hiện ảnh đã gộp lớp (đọc trực tiếp nên nhanh cả với file nhiều GB); nếu file không có ảnh gộp (lưu không bật Maximize Compatibility) thì hiện ảnh thu nhỏ nhúng sẵn.
- Định dạng lạ mà mpv phát được thì thêm đuôi vào **Cài đặt → Định dạng bổ sung**.
- Video dạng `blob:` (nhiều trang tự dựng player) không lộ link thật nên không xem trước được; link tới trang video của các site được hỗ trợ thì được.
- Video đang phát sẵn trong trang sẽ không bị xem trước chồng lên.
- Site như YouTube/TikTok thay đổi liên tục; nếu một site hỏng, cập nhật `yt-dlp.exe` trong thư mục cài đặt.
- Windows 11: Explorer dùng tab; HoverPeDES dò đường dẫn thư mục qua Shell. Nếu thư mục đặc biệt (This PC, Search results) không nhận, đó là giới hạn đã biết.
- Hiện chỉ hỗ trợ Windows 10/11 64-bit, Chrome/Edge/Brave (Chromium). Chưa có Firefox.

## Tự build ra file .exe (không cần cài gì trên máy)

1. Tạo repo GitHub, đẩy toàn bộ thư mục này lên (nhánh `main`).
2. Vào tab **Actions** → workflow **build** chạy tự động (hoặc bấm **Run workflow**).
3. Tải file ở mục **Artifacts** (`HoverPeDES-Setup-1.0.0.exe`, bản portable `.zip`, và `HoverPeDES-extension-1.0.0.zip`).
4. Phát hành công khai: gắn tag, ví dụ `git tag v1.0.1 && git push --tags`. Workflow sẽ tự tạo **Release** kèm các file trên.

Build thủ công trên Windows: cài .NET 8 SDK + Inno Setup 6, rồi chạy `./build.ps1`.

## Đăng tiện ích lên Chrome Web Store (tùy chọn)

Nộp file `HoverPeDES-extension-x.y.z.zip` ở Chrome Developer Dashboard (phí đăng ký một lần 5 USD). Chính sách quyền riêng tư: xem `PRIVACY.md`. Ghi rõ trong mô tả rằng tiện ích cần ứng dụng HoverPeDES trên máy để xem trước video.

## Bảo mật & riêng tư

Mọi thứ chạy trên máy bạn. Ứng dụng chỉ lắng nghe ở `127.0.0.1:47653`, chỉ chấp nhận yêu cầu thay đổi trạng thái từ tiện ích trình duyệt (kiểm tra Origin và Host), không thu thập hay gửi dữ liệu nào đi. Chi tiết ở `PRIVACY.md`.

## Cấu trúc mã nguồn

```
src/HoverPeDES/     Ứng dụng C# (.NET 8, WinForms) + libmpv
  HoverWatcher.cs    dò file dưới con trỏ trong Explorer/Desktop (UI Automation + Shell)
  PreviewForm.cs     khung nổi không viền, nhúng mpv, phím tắt toàn cục
  LocalServer.cs     máy chủ loopback cho tiện ích trình duyệt
  Mpv.cs             lớp bọc libmpv
extension/         Tiện ích Chrome/Edge (Manifest V3), có tiếng Anh + Việt
installer/         Kịch bản Inno Setup (cài song ngữ Anh/Việt)
.github/workflows/ Build tự động ra .exe
```

## Giấy phép

Mã nguồn HoverPeDES: MIT (xem `LICENSE`). Thành phần bên thứ ba (mpv, FFmpeg, yt-dlp, .NET): xem `THIRD-PARTY-NOTICES.md`.

---

## English

Hover a video or image and a floating preview starts playing instantly. Two places it works:

| Where | How | Needs |
|---|---|---|
| **Folders / Desktop** (Explorer) | Hover a video or image file; a floating window plays it with **mpv** (hundreds of formats: video mp4, mkv, avi, mov, flv, wmv, webm, ts, rmvb, vob ...; audio mp3, flac, wav, m4a, ogg, opus, wma, ape ... with a visualizer; images jpg, png, gif, webp, avif, heic, tiff, ico ...; text and code txt, md, json, csv, log, py, js, srt ...; documents PDF, SVG, Word, Excel, PowerPoint; camera RAW CR2/NEF/ARW/DNG and PSD) | HoverPeDES app |
| **Websites** | Hover an image: enlarged in the page. Hover a video or a video link: the app plays it (direct .mp4/.webm/.m3u8, YouTube, TikTok, Vimeo, Dailymotion, Bilibili, Twitch clips, Facebook/Instagram reels ...) | App + Chrome/Edge extension |

### Install

1. Download `HoverPeDES-Setup-x.y.z.exe` from **Releases** and run it (no admin rights needed, English and Vietnamese).
2. Start HoverPeDES (it lives in the system tray).
3. For websites: right-click the tray icon → **Install browser extension...** and follow the steps (Chrome/Edge → Extensions → Developer mode → Load unpacked → pick the `extension` folder), or install from the store once it is published.

Windows SmartScreen may warn about an unknown publisher because the installer is not code-signed. Click **More info → Run anyway**.

### Hotkeys while previewing

`Ctrl+Alt+Space` pause/play · `Ctrl+Alt+←/→` seek 5 s · `Ctrl+Alt+M` mute. Video and audio play **with sound** by default (tick **Mute** in Settings to silence them). For PDFs, move the mouse onto the preview window and use the wheel to scroll. Clicking (opening the file) closes the preview. Settings (delay, size, mute, start position, language, autostart) are under tray menu → **Settings...**.

### Limitations

DRM video (Netflix, Disney+ ...) cannot be previewed. PDF and SVG use the Microsoft Edge WebView2 Runtime (included in Windows 11 and up-to-date Windows 10; the app shows an install link if it is missing). Word, Excel and PowerPoint (docx, xlsx, pptx, odt, ods, odp) show the extracted **text only**, without layout or images; legacy .doc, .xls and .ppt are not supported. Camera RAW photos (CR2, NEF, ARW, DNG, ORF, RW2, RAF ...) show the full-size JPEG preview embedded in the file, not a developed RAW. PSD and PSB show the merged image, read directly so even multi-GB files are quick; if the file has no merged image (saved without Maximize Compatibility) the embedded thumbnail is shown. For any odd format mpv can play, add its extension under **Settings → Extra file types**. `blob:` videos hide their real URL, so they cannot be previewed either (links to supported video pages work). Videos already playing in the page are not overlaid. Sites like YouTube/TikTok change often; update `yt-dlp.exe` in the install folder if one breaks. Windows 10/11 x64 and Chromium browsers only for now.

### Build the .exe yourself

Push this folder to a GitHub repo; the **build** workflow produces the installer, a portable zip and the extension zip under **Actions → Artifacts**. Push a tag such as `v1.0.1` to publish a GitHub **Release** automatically. Locally on Windows: install .NET 8 SDK and Inno Setup 6, then run `./build.ps1`.

### Privacy & license

Everything runs locally; nothing is collected or sent anywhere (see `PRIVACY.md`). HoverPeDES is MIT licensed; third-party components are listed in `THIRD-PARTY-NOTICES.md`.
