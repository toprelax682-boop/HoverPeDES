// HoverPeDES content script.
// - Hover an image  -> enlarged preview shown right in the page (no app needed).
// - Hover a video / video link -> asks the HoverPeDES desktop app to play it
//   (any codec through mpv; YouTube, TikTok, Vimeo... through yt-dlp).

(() => {
  if (window.__hoverPeDESLoaded) return;
  window.__hoverPeDESLoaded = true;

  const DEFAULTS = { enabled: true, images: true, videos: true, delay: 350, disabledHosts: [] };
  let cfg = { ...DEFAULTS };

  try {
    chrome.storage.sync.get(DEFAULTS, (v) => { cfg = { ...DEFAULTS, ...v }; });
    chrome.storage.onChanged.addListener((changes) => {
      for (const k in changes) cfg[k] = changes[k].newValue;
    });
  } catch (_) { /* extension reloaded; page needs refresh */ }

  const VIDEO_EXT = /\.(mp4|m4v|webm|mkv|mov|avi|flv|wmv|ts|m2ts|m3u8|mpd|ogv|3gp|rmvb)$/i;
  const IMAGE_EXT = /\.(jpe?g|png|gif|webp|avif|bmp|svg)$/i;

  // Sites where a link reliably points to a single playable video (handled by yt-dlp).
  const VIDEO_SITES = [
    (u) => /(^|\.)youtube\.com$/.test(u.hostname) && (u.pathname === "/watch" || /^\/(shorts|live|embed)\//.test(u.pathname)),
    (u) => u.hostname === "youtu.be" && u.pathname.length > 1,
    (u) => /(^|\.)tiktok\.com$/.test(u.hostname) && /\/video\/\d+/.test(u.pathname),
    (u) => u.hostname === "vimeo.com" && /^\/\d+/.test(u.pathname),
    (u) => /(^|\.)dailymotion\.com$/.test(u.hostname) && /^\/video\//.test(u.pathname),
    (u) => /(^|\.)bilibili\.com$/.test(u.hostname) && /^\/video\//.test(u.pathname),
    (u) => /(^|\.)twitch\.tv$/.test(u.hostname) && /(\/clip\/|\/videos\/)/.test(u.pathname),
    (u) => u.hostname === "clips.twitch.tv",
    (u) => u.hostname === "streamable.com" && u.pathname.length > 1,
    (u) => /(^|\.)facebook\.com$/.test(u.hostname) && /(\/watch|\/videos\/|\/reel\/)/.test(u.pathname),
    (u) => /(^|\.)instagram\.com$/.test(u.hostname) && /^\/reels?\//.test(u.pathname),
  ];

  function parse(href) {
    try { return new URL(href, location.href); } catch (_) { return null; }
  }
  function isHttp(u) { return u && (u.protocol === "http:" || u.protocol === "https:"); }

  function isVideoLink(href) {
    const u = parse(href);
    if (!isHttp(u)) return false;
    return VIDEO_EXT.test(u.pathname) || VIDEO_SITES.some((f) => f(u));
  }
  function isImageLink(href) {
    const u = parse(href);
    return isHttp(u) && IMAGE_EXT.test(u.pathname);
  }

  function biggestFromSrcset(img) {
    const set = img.getAttribute("srcset");
    if (!set) return null;
    let best = null, bestW = 0;
    for (const part of set.split(",")) {
      const [url, d] = part.trim().split(/\s+/);
      const w = d && /w$/.test(d) ? parseInt(d, 10) : d && /x$/.test(d) ? parseFloat(d) * 1000 : 0;
      if (url && w >= bestW) { best = url; bestW = w; }
    }
    return best ? parse(best)?.href : null;
  }

  function bgImageUrl(el) {
    const bg = getComputedStyle(el).backgroundImage;
    const m = bg && bg.match(/url\((['"]?)(.*?)\1\)/);
    return m ? parse(m[2])?.href : null;
  }

  function videoSource(v) {
    const src = v.currentSrc || v.src || (v.querySelector("source[src]") || {}).src;
    if (!src || src.startsWith("blob:") || src.startsWith("data:")) return null;
    return isHttp(parse(src)) ? src : null;
  }

  // Inspect one element (and a few ancestors) and decide what, if anything, to preview.
  function inspect(el) {
    const anchor = el.closest ? el.closest("a[href]") : null;
    if (anchor && cfg.videos && isVideoLink(anchor.href)) return { kind: "video", url: parse(anchor.href).href };

    let n = el;
    for (let i = 0; i < 6 && n && n.nodeType === 1 && n !== document.documentElement; i++, n = n.parentElement) {
      const tag = n.tagName;
      if (tag === "VIDEO") {
        if (!cfg.videos || !n.paused) return null; // already playing in the page
        const src = videoSource(n);
        return src ? { kind: "video", url: src } : null;
      }
      if (tag === "IMG" || tag === "PICTURE") {
        if (!cfg.images) return null;
        const img = tag === "PICTURE" ? n.querySelector("img") : n;
        if (!img) return null;
        const r = img.getBoundingClientRect();
        if (r.width < 48 || r.height < 48) return null;
        if (anchor && isImageLink(anchor.href)) return { kind: "image", url: parse(anchor.href).href };
        const big = biggestFromSrcset(img) || img.currentSrc || img.src;
        if (!big || big.startsWith("data:")) return null;
        const worthIt = r.width < 320 || (img.naturalWidth && img.naturalWidth > r.width * 1.15);
        return worthIt ? { kind: "image", url: big } : null;
      }
      if (cfg.images && n !== document.body) {
        const bg = bgImageUrl(n);
        if (bg) {
          const r = n.getBoundingClientRect();
          if (r.width >= 64 && r.height >= 64) return { kind: "image", url: bg };
        }
      }
    }
    if (anchor && cfg.images && isImageLink(anchor.href)) return { kind: "image", url: parse(anchor.href).href };
    return null;
  }

  function resolveAt(x, y) {
    const stack = document.elementsFromPoint(x, y).slice(0, 8);
    for (const el of stack) {
      if (el.id === "hoverpedes-host") continue;
      const t = inspect(el);
      if (t) return t;
    }
    return null;
  }

  // ------------------------------------------------------------------ image overlay

  let host = null, imgEl = null;

  function ensureOverlay() {
    if (host) return;
    host = document.createElement("div");
    host.id = "hoverpedes-host";
    host.style.cssText = "all:initial;position:fixed;left:0;top:0;width:0;height:0;z-index:2147483647;pointer-events:none;";
    const root = host.attachShadow({ mode: "closed" });
    const style = document.createElement("style");
    style.textContent = `
      .box{position:fixed;display:none;background:#14141a;border-radius:12px;padding:4px;
           box-shadow:0 12px 40px rgba(0,0,0,.45),0 0 0 1px rgba(255,255,255,.08);}
      .box img{display:block;border-radius:9px;max-width:min(62vw,900px);max-height:78vh;object-fit:contain;background:#000}
      .cap{font:12px/1.3 system-ui,sans-serif;color:#cfcfd6;padding:5px 6px 2px}`;
    const box = document.createElement("div");
    box.className = "box";
    imgEl = document.createElement("img");
    imgEl.referrerPolicy = "no-referrer";
    const cap = document.createElement("div");
    cap.className = "cap";
    box.append(imgEl, cap);
    root.append(style, box);
    host._box = box; host._cap = cap;
    (document.documentElement).appendChild(host);
  }

  function placeOverlay(x, y) {
    if (!host || host._box.style.display === "none") return;
    const box = host._box;
    const w = box.offsetWidth, h = box.offsetHeight, m = 24;
    let left = x + m, top = y + m;
    if (left + w > innerWidth - 8) left = x - m - w;
    if (top + h > innerHeight - 8) top = innerHeight - 8 - h;
    box.style.left = Math.max(8, left) + "px";
    box.style.top = Math.max(8, top) + "px";
  }

  function showImage(url) {
    ensureOverlay();
    const token = url;
    host._token = token;
    imgEl.onload = () => {
      if (host._token !== token) return;
      host._cap.textContent = imgEl.naturalWidth + " × " + imgEl.naturalHeight;
      host._box.style.display = "block";
      placeOverlay(lastX, lastY);
    };
    imgEl.onerror = () => { if (host._token === token) hideImage(); };
    imgEl.src = url;
  }

  function hideImage() {
    if (!host) return;
    host._token = null;
    host._box.style.display = "none";
    imgEl.removeAttribute("src");
  }

  // ------------------------------------------------------------------ hover state machine

  let lastX = 0, lastY = 0, timer = null, current = null;

  function sameTarget(a, b) { return (!a && !b) || (a && b && a.kind === b.kind && a.url === b.url); }

  function hide() {
    if (!current) return;
    if (current.kind === "image") hideImage();
    else send({ type: "hide" });
    current = null;
  }

  function show(t) {
    current = t;
    if (t.kind === "image") showImage(t.url);
    else send({ type: "show", url: t.url, referrer: location.href });
  }

  function send(msg) {
    try { chrome.runtime.sendMessage(msg); } catch (_) { /* extension was reloaded */ }
  }

  function allowed() {
    return cfg.enabled && !cfg.disabledHosts.includes(location.hostname) && !document.fullscreenElement;
  }

  function settle() {
    timer = null;
    const t = allowed() ? resolveAt(lastX, lastY) : null;
    if (sameTarget(t, current)) return;
    hide();
    if (t) show(t);
  }

  document.addEventListener("mousemove", (e) => {
    lastX = e.clientX; lastY = e.clientY;
    if (e.buttons) { hide(); return; }
    if (current && current.kind === "image") placeOverlay(lastX, lastY);
    if (!allowed() && !current) return;
    clearTimeout(timer);
    timer = setTimeout(settle, current ? 120 : Math.max(100, cfg.delay | 0));
  }, { passive: true, capture: true });

  const cancel = () => { clearTimeout(timer); timer = null; hide(); };
  document.addEventListener("scroll", cancel, { passive: true, capture: true });
  document.addEventListener("mousedown", cancel, true);
  document.addEventListener("keydown", cancel, true);
  document.documentElement.addEventListener("mouseleave", cancel);
  window.addEventListener("blur", cancel);
  document.addEventListener("visibilitychange", () => { if (document.hidden) cancel(); });
})();
