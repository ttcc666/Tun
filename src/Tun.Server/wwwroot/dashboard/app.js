const tokenInput = document.querySelector("#token");
const saveTokenButton = document.querySelector("#saveToken");
const refreshButton = document.querySelector("#refresh");
const resetFormButton = document.querySelector("#resetForm");
const form = document.querySelector("#tunnelForm");
const rows = document.querySelector("#tunnelRows");
const toast = document.querySelector("#toast");

const configuredCount = document.querySelector("#configuredCount");
const onlineCount = document.querySelector("#onlineCount");
const requestCount = document.querySelector("#requestCount");
const lastRefresh = document.querySelector("#lastRefresh");

let state = { configured: [], online: [] };

tokenInput.value = localStorage.getItem("tun.managementToken") || "dev-token";

saveTokenButton.addEventListener("click", () => {
  localStorage.setItem("tun.managementToken", tokenInput.value);
  showToast("Token 已保存");
  load();
});

refreshButton.addEventListener("click", load);
resetFormButton.addEventListener("click", () => {
  form.reset();
  document.querySelector("#enabled").checked = true;
  document.querySelector("#clientId").value = "dev-client"; // 设置默认 ClientId
});
form.addEventListener("submit", saveTunnel);

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  headers.set("X-Tun-Token", tokenInput.value);
  if (options.body) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, { ...options, headers });
  if (response.status === 401) {
    throw new Error("Token 无效或未提供");
  }

  if (!response.ok) {
    let message = `请求失败: ${response.status}`;
    try {
      const problem = await response.json();
      message = problem.error || message;
    } catch {
    }
    throw new Error(message);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function load() {
  try {
    state = await api("/api/config/tunnels");
    render();
  } catch (error) {
    showToast(error.message);
  }
}

async function saveTunnel(event) {
  event.preventDefault();
  const data = new FormData(form);
  const payload = {
    tunnelId: data.get("tunnelId")?.trim(),
    clientId: data.get("clientId")?.trim(),
    localUrl: data.get("localUrl")?.trim(),
    enabled: document.querySelector("#enabled").checked,
    description: data.get("description")?.trim()
  };

  try {
    await api("/api/config/tunnels", {
      method: "POST",
      body: JSON.stringify(payload)
    });
    form.reset();
    document.querySelector("#enabled").checked = true;
    document.querySelector("#clientId").value = "dev-client"; // 设置默认 ClientId
    showToast("Tunnel 配置已保存");
    await load();
  } catch (error) {
    showToast(error.message);
  }
}

async function deleteTunnel(tunnelId) {
  if (!confirm(`删除 tunnel '${tunnelId}'?`)) {
    return;
  }

  try {
    await api(`/api/config/tunnels/${encodeURIComponent(tunnelId)}`, { method: "DELETE" });
    showToast("Tunnel 已删除");
    await load();
  } catch (error) {
    showToast(error.message);
  }
}

function editTunnel(tunnel) {
  document.querySelector("#tunnelId").value = tunnel.tunnelId;
  document.querySelector("#clientId").value = tunnel.clientId;
  document.querySelector("#localUrl").value = tunnel.localUrl;
  document.querySelector("#description").value = tunnel.description || "";
  document.querySelector("#enabled").checked = tunnel.enabled;
}

function render() {
  const onlineByTunnel = new Map((state.online || []).map(item => [item.tunnelId.toLowerCase(), item]));
  const totalRequests = (state.online || []).reduce((sum, item) => sum + Number(item.requestCount || 0), 0);

  configuredCount.textContent = String((state.configured || []).length);
  onlineCount.textContent = String((state.online || []).length);
  requestCount.textContent = String(totalRequests);
  lastRefresh.textContent = new Date().toLocaleTimeString();

  rows.innerHTML = "";
  for (const tunnel of state.configured || []) {
    const online = onlineByTunnel.get(tunnel.tunnelId.toLowerCase());
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td><strong>${escapeHtml(tunnel.tunnelId)}</strong><br><span>${escapeHtml(tunnel.description || "")}</span></td>
      <td>${escapeHtml(tunnel.localUrl)}</td>
      <td><a class="tunnel-link" href="${escapeAttr(buildTunnelUrl(tunnel.tunnelId))}" target="_blank" rel="noreferrer">打开</a></td>
      <td><span class="status ${online ? "online" : "offline"}">${online ? "在线" : tunnel.enabled ? "离线" : "停用"}</span></td>
      <td>${online ? online.requestCount : 0}</td>
      <td>
        <div class="row-actions">
          <button type="button" class="secondary" data-edit="${escapeAttr(tunnel.tunnelId)}">编辑</button>
          <button type="button" class="danger" data-delete="${escapeAttr(tunnel.tunnelId)}">删除</button>
        </div>
      </td>
    `;
    rows.appendChild(tr);
  }

  rows.querySelectorAll("[data-edit]").forEach(button => {
    button.addEventListener("click", () => {
      const tunnel = state.configured.find(item => item.tunnelId === button.dataset.edit);
      if (tunnel) {
        editTunnel(tunnel);
      }
    });
  });
  rows.querySelectorAll("[data-delete]").forEach(button => {
    button.addEventListener("click", () => deleteTunnel(button.dataset.delete));
  });
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add("show");
  window.clearTimeout(showToast.timeoutId);
  showToast.timeoutId = window.setTimeout(() => toast.classList.remove("show"), 2800);
}

function buildTunnelUrl(tunnelId) {
  if (state.baseDomain) {
    const scheme = window.location.protocol;
    return `${scheme}//${encodeURIComponent(tunnelId)}.${state.baseDomain}/`;
  }

  // Fallback
  const origin = String(state.publicOrigin || window.location.origin).replace(/\/$/, "");
  return `${origin}/t/${encodeURIComponent(tunnelId)}/`;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function escapeAttr(value) {
  return escapeHtml(value).replaceAll("`", "&#096;");
}

load();
window.setInterval(load, 5000);
