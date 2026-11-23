// HoverPeDES background service worker.
// Relays "show video" / "hide" requests from content scripts to the desktop app
// listening on 127.0.0.1:47653, and reports whether the app is reachable.

const BASE = "http://127.0.0.1:47653";

function setBadge(connected) {
  chrome.action.setBadgeText({ text: connected ? "" : "!" });
  if (!connected) chrome.action.setBadgeBackgroundColor({ color: "#d9534f" });
}

async function post(path, body) {
  try {
    const res = await fetch(BASE + path, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-HoverPeDES": "1" },
      body: JSON.stringify(body || {}),
    });
    setBadge(res.ok);
    return res.ok;
  } catch (e) {
    setBadge(false);
    return false;
  }
}

async function ping() {
  try {
    const res = await fetch(BASE + "/ping", { cache: "no-store" });
    if (!res.ok) throw new Error("bad status");
    const data = await res.json();
    setBadge(true);
    return { connected: data.app === "HoverPeDES", appEnabled: data.enabled !== false };
  } catch (e) {
    setBadge(false);
    return { connected: false, appEnabled: false };
  }
}

chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (!msg || typeof msg.type !== "string") return;
  if (msg.type === "show") {
    post("/show", { url: msg.url, referrer: msg.referrer || "" });
  } else if (msg.type === "hide") {
    post("/hide");
  } else if (msg.type === "ping") {
    ping().then(sendResponse);
    return true; // async response
  }
});

chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.sync.get(null, (v) => {
    const defaults = { enabled: true, images: true, videos: true, delay: 350, disabledHosts: [] };
    chrome.storage.sync.set({ ...defaults, ...v });
  });
  ping();
});
