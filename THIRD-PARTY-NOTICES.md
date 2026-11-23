# Third-party components / Thành phần bên thứ ba

HoverPeDES's installer bundles these unmodified components. Each keeps its own license.

| Component | Use | License | Source |
|---|---|---|---|
| mpv / libmpv (`libmpv-2.dll`) | Video and image playback engine | LGPL-2.1+ (the CI downloads the LGPL build; the standard build is GPL-2.0+) | https://mpv.io · builds: https://github.com/shinchiro/mpv-winbuild-cmake |
| FFmpeg (inside libmpv) | Decoding | LGPL-2.1+ / GPL-2.0+ depending on build | https://ffmpeg.org |
| yt-dlp (`yt-dlp.exe`) | Resolves YouTube, TikTok, Vimeo ... links | The Unlicense | https://github.com/yt-dlp/yt-dlp |
| .NET 8 runtime (self-contained) | Application runtime | MIT | https://github.com/dotnet/runtime |
| Windows Presentation Foundation (UI Automation client) | Finds the file under the cursor | MIT | https://github.com/dotnet/wpf |

`libmpv-2.dll` is loaded dynamically as a separate file next to `HoverPeDES.exe`, so you can replace it with any compatible build. Source code for mpv and FFmpeg is available at the links above.

If you redistribute HoverPeDES, keep this file and the license of each component with the package.
