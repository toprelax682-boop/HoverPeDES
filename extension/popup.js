const DEFAULTS = { enabled: true, images: true, videos: true, delay: 350, disabledHosts: [] };
const $ = (id) => document.getElementById(id);
const msg = (k, subs) => chrome.i18n.getMessage(k, subs) || k;

document.querySelectorAll("[data-i18n]").forEach((el) => { el.textContent = msg(el.dataset.i18n); });

let cfg = { ...DEFAULTS };
let host = "";

function save(patch) {
  cfg = { ...cfg, ...patch };
  chrome.storage.sync.set(patch);
}

function render() {
  $("enabled").checked = cfg.enabled;
  $("images").checked = cfg.images;
  $("videos").checked = cfg.videos;
  $("delay").value = cfg.delay;
  $("delayOut").textContent = cfg.delay + " ms";
  $("site").checked = host ? !cfg.disabledHosts.includes(host) : true;
  $("site").disabled = !host;
}

chrome.storage.sync.get(DEFAULTS, async (v) => {
  cfg = { ...DEFAULTS, ...v };
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab && tab.url) {
      const u = new URL(tab.url);
      if (u.protocol === "http:" || u.protocol === "https:") host = u.hostname;
    }
  } catch (_) {}
  $("siteLabel").textContent = host ? msg("optSite", [host]) : msg("optSiteNone");
  render();
});

$("enabled").addEventListener("change", (e) => save({ enabled: e.target.checked }));
$("images").addEventListener("change", (e) => save({ images: e.target.checked }));
$("videos").addEventListener("change", (e) => save({ videos: e.target.checked }));
$("delay").addEventListener("input", (e) => {
  $("delayOut").textContent = e.target.value + " ms";
  save({ delay: parseInt(e.target.value, 10) });
});
$("site").addEventListener("change", (e) => {
  const list = new Set(cfg.disabledHosts);
  if (e.target.checked) list.delete(host); else list.add(host);
  save({ disabledHosts: [...list] });
});

chrome.runtime.sendMessage({ type: "ping" }, (res) => {
  const ok = !!(res && res.connected);
  $("dot").classList.toggle("ok", ok);
  $("status").textContent = ok ? msg("statusOk") : msg("statusOff");
  void chrome.runtime.lastError;
});
