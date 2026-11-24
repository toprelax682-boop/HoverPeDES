# Privacy Policy / Chính sách quyền riêng tư

**English**

HoverPeDES does not collect, store, sell or transmit any personal data.

- The browser extension reads the page you are viewing only to find the image or video under your mouse pointer. Images are shown in the page. For videos, the extension sends the video URL and the page address (as referrer) to the HoverPeDES app on your own computer at `127.0.0.1:47653`. Nothing is sent to any other server by the extension.
- Extension settings (on/off, delay, disabled sites) are kept in your browser's synced storage (`chrome.storage.sync`).
- The desktop app keeps settings and a small log under `%APPDATA%\HoverPeDES`. It does not upload them.
- PDF and SVG previews are rendered locally by Microsoft Edge WebView2, with its profile kept in `%APPDATA%\HoverPeDES\webview2`. An SVG file that references external resources may cause those resources to be fetched. Word, Excel, PowerPoint and RAW previews are read locally and never leave your computer.
- When you preview a web video, the app (mpv, and yt-dlp for sites such as YouTube) downloads that video directly from the website, just as your browser would.

The extension requests: `storage` (save settings), `activeTab` (show the current site in the popup) and access to `http://127.0.0.1:47653/*` (talk to the desktop app). The content script runs on all sites so it can detect media under the pointer.

**Tiếng Việt**

HoverPeDES không thu thập, lưu trữ, bán hay gửi đi bất kỳ dữ liệu cá nhân nào.

- Tiện ích trình duyệt chỉ đọc trang bạn đang xem để tìm ảnh hoặc video dưới con trỏ. Ảnh được hiện ngay trong trang. Với video, tiện ích gửi địa chỉ video và địa chỉ trang (referrer) tới ứng dụng HoverPeDES trên chính máy bạn qua `127.0.0.1:47653`. Tiện ích không gửi gì tới máy chủ nào khác.
- Cài đặt tiện ích (bật/tắt, độ trễ, trang bị tắt) lưu trong bộ nhớ đồng bộ của trình duyệt (`chrome.storage.sync`).
- Ứng dụng lưu cài đặt và một file nhật ký nhỏ trong `%APPDATA%\HoverPeDES`, không tải lên đâu cả.
- Xem trước PDF và SVG do Microsoft Edge WebView2 hiển thị ngay trên máy, hồ sơ lưu ở `%APPDATA%\HoverPeDES\webview2`. File SVG có tham chiếu tài nguyên bên ngoài có thể khiến tài nguyên đó được tải. Xem trước Word, Excel, PowerPoint và ảnh RAW đều đọc cục bộ, không gửi đi đâu.
- Khi xem trước video trên web, ứng dụng (mpv, và yt-dlp với các site như YouTube) tải video trực tiếp từ website, giống như trình duyệt của bạn.

Quyền tiện ích yêu cầu: `storage` (lưu cài đặt), `activeTab` (hiện trang hiện tại trong popup) và truy cập `http://127.0.0.1:47653/*` (nói chuyện với ứng dụng). Content script chạy trên mọi trang để nhận biết ảnh/video dưới con trỏ.
