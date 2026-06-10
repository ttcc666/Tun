<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { api, tokenStorage } from '../services/api'
import type { TunnelsData, TunnelConfig, UpsertTunnelRequest } from '../services/types'

// State
const data = ref<TunnelsData | null>(null)
const loading = ref(false)
const token = ref(tokenStorage.get())
const toastMessage = ref('')
const showToast = ref(false)

// Form state
const form = ref<UpsertTunnelRequest>({
  tunnelId: '',
  clientId: 'dev-client',
  localUrl: '',
  enabled: true,
  description: '',
})

// Computed
const configured = computed(() => data.value?.configured || [])
const online = computed(() => data.value?.online || [])
const baseDomain = computed(() => data.value?.baseDomain || '')

const configuredCount = computed(() => configured.value.length)
const onlineCount = computed(() => online.value.length)
const totalRequests = computed(() =>
  online.value.reduce((sum: number, item: { requestCount?: number }) => sum + (item.requestCount || 0), 0)
)

const onlineMap = computed(() => {
  const map = new Map()
  online.value.forEach((item: { tunnelId: string }) => {
    map.set(item.tunnelId.toLowerCase(), item)
  })
  return map
})

const lastRefresh = ref('')

// Methods
const load = async () => {
  loading.value = true
  try {
    data.value = await api.getTunnels()
    lastRefresh.value = new Date().toLocaleTimeString()
  } catch (error) {
    toast(error instanceof Error ? error.message : '加载失败')
  } finally {
    loading.value = false
  }
}

const save = async () => {
  try {
    await api.upsertTunnel(form.value)
    toast(`Tunnel "${form.value.tunnelId}" 配置已保存`)
    resetForm()
    await load()
  } catch (error) {
    toast(error instanceof Error ? error.message : '保存失败')
  }
}

const deleteTunnel = async (tunnelId: string) => {
  if (!confirm(`删除 tunnel '${tunnelId}'?`)) {
    return
  }
  try {
    await api.deleteTunnel(tunnelId)
    toast(`Tunnel "${tunnelId}" 已删除`)
    await load()
  } catch (error) {
    toast(error instanceof Error ? error.message : '删除失败')
  }
}

const editTunnel = (tunnel: TunnelConfig) => {
  form.value = {
    tunnelId: tunnel.tunnelId,
    clientId: tunnel.clientId,
    localUrl: tunnel.localUrl,
    enabled: tunnel.enabled,
    description: tunnel.description || '',
  }
}

const resetForm = () => {
  form.value = {
    tunnelId: '',
    clientId: 'dev-client',
    localUrl: '',
    enabled: true,
    description: '',
  }
}

const saveToken = () => {
  tokenStorage.set(token.value)
  toast('Token 已保存')
  load()
}

const buildPublicUrl = (tunnelId: string) => {
  if (baseDomain.value) {
    const scheme = window.location.protocol
    return `${scheme}//${tunnelId}.${baseDomain.value}/`
  }
  const origin = data.value?.publicOrigin || window.location.origin
  return `${origin.replace(/\/$/, '')}/t/${encodeURIComponent(tunnelId)}/`
}

const getStatus = (tunnel: TunnelConfig) => {
  const onlineStatus = onlineMap.value.get(tunnel.tunnelId.toLowerCase())
  if (onlineStatus) return 'online'
  return tunnel.enabled ? 'offline' : 'disabled'
}

const getStatusText = (tunnel: TunnelConfig) => {
  const status = getStatus(tunnel)
  if (status === 'online') return '在线'
  if (status === 'disabled') return '停用'
  return '离线'
}

const toast = (message: string) => {
  toastMessage.value = message
  showToast.value = true
  setTimeout(() => {
    showToast.value = false
  }, 2800)
}

// Lifecycle
onMounted(() => {
  load()
  setInterval(load, 5000)
})
</script>

<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Topbar -->
    <header class="bg-white border-b border-gray-200">
      <div class="flex items-end justify-between gap-6 px-8 py-6">
        <div>
          <h1 class="text-3xl font-bold text-gray-900">Tunnel Dashboard</h1>
          <p class="text-sm text-gray-500 mt-1">管理您的隧道配置</p>
        </div>

        <!-- Token Panel -->
        <div class="w-96">
          <label class="block text-sm text-gray-500 mb-2">
            Management Token
          </label>
          <div class="flex gap-2">
            <input
              v-model="token"
              type="text"
              class="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
              placeholder="dev-token"
            />
            <button
              @click="saveToken"
              class="px-4 py-2 bg-teal-600 text-white font-medium rounded-md hover:bg-teal-700 transition-colors"
            >
              保存
            </button>
          </div>
        </div>
      </div>
    </header>

    <!-- Main Content -->
    <main class="max-w-7xl mx-auto px-8 py-6">
      <!-- Stats Cards -->
      <div class="grid grid-cols-4 gap-3 mb-4">
        <div class="bg-white border border-gray-200 rounded-lg p-4">
          <span class="text-sm text-gray-500">已配置</span>
          <strong class="block text-2xl font-semibold mt-1">{{ configuredCount }}</strong>
        </div>
        <div class="bg-white border border-gray-200 rounded-lg p-4">
          <span class="text-sm text-gray-500">在线</span>
          <strong class="block text-2xl font-semibold mt-1">{{ onlineCount }}</strong>
        </div>
        <div class="bg-white border border-gray-200 rounded-lg p-4">
          <span class="text-sm text-gray-500">请求总数</span>
          <strong class="block text-2xl font-semibold mt-1">{{ totalRequests }}</strong>
        </div>
        <div class="bg-white border border-gray-200 rounded-lg p-4">
          <span class="text-sm text-gray-500">最后刷新</span>
          <strong class="block text-sm font-semibold mt-1">{{ lastRefresh }}</strong>
        </div>
      </div>

      <!-- Layout -->
      <div class="grid grid-cols-[360px_1fr] gap-4 items-start">
        <!-- Form Panel -->
        <div class="bg-white border border-gray-200 rounded-lg p-5">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold">新增 / 编辑 Tunnel</h2>
            <button
              @click="load"
              class="px-3 py-1.5 text-sm bg-gray-100 hover:bg-gray-200 rounded-md transition-colors"
            >
              刷新
            </button>
          </div>

          <form @submit.prevent="save" class="space-y-3">
            <div>
              <label class="block text-sm text-gray-500 mb-1.5">Tunnel ID</label>
              <input
                v-model="form.tunnelId"
                required
                class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
                placeholder="demo"
              />
            </div>

            <div>
              <label class="block text-sm text-gray-500 mb-1.5">Local URL</label>
              <input
                v-model="form.localUrl"
                required
                class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
                placeholder="http://localhost:3000"
              />
            </div>

            <div>
              <label class="block text-sm text-gray-500 mb-1.5">Description</label>
              <input
                v-model="form.description"
                class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
                placeholder="可选描述"
              />
            </div>

            <div class="flex items-center gap-2 py-3">
              <input
                v-model="form.enabled"
                type="checkbox"
                id="enabled"
                class="w-4 h-4 text-teal-600 border-gray-300 rounded focus:ring-teal-500"
              />
              <label for="enabled" class="text-sm text-gray-700">启用</label>
            </div>

            <div class="flex gap-2 pt-2">
              <button
                type="submit"
                class="flex-1 px-4 py-2 bg-teal-600 text-white font-medium rounded-md hover:bg-teal-700 transition-colors"
              >
                保存
              </button>
              <button
                type="button"
                @click="resetForm"
                class="px-4 py-2 bg-gray-100 text-gray-700 font-medium rounded-md hover:bg-gray-200 transition-colors"
              >
                重置
              </button>
            </div>
          </form>
        </div>

        <!-- Table Panel -->
        <div class="bg-white border border-gray-200 rounded-lg p-5">
          <h2 class="text-lg font-semibold mb-4">Tunnel 列表</h2>

          <div class="overflow-x-auto">
            <table class="w-full">
              <thead>
                <tr class="border-b border-gray-200">
                  <th class="text-left text-sm font-semibold text-gray-500 pb-3">Tunnel</th>
                  <th class="text-left text-sm font-semibold text-gray-500 pb-3">Local URL</th>
                  <th class="text-left text-sm font-semibold text-gray-500 pb-3">公网</th>
                  <th class="text-left text-sm font-semibold text-gray-500 pb-3">状态</th>
                  <th class="text-left text-sm font-semibold text-gray-500 pb-3">请求数</th>
                  <th class="text-left text-sm font-semibold text-gray-500 pb-3">操作</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="tunnel in configured"
                  :key="tunnel.tunnelId"
                  class="border-b border-gray-200 last:border-0"
                >
                  <td class="py-3">
                    <strong class="text-sm">{{ tunnel.tunnelId }}</strong>
                    <br />
                    <span class="text-xs text-gray-500">{{ tunnel.description }}</span>
                  </td>
                  <td class="text-sm">{{ tunnel.localUrl }}</td>
                  <td>
                    <a
                      :href="buildPublicUrl(tunnel.tunnelId)"
                      target="_blank"
                      rel="noreferrer"
                      class="text-sm text-teal-600 font-medium hover:text-teal-700 hover:underline"
                    >
                      打开
                    </a>
                  </td>
                  <td>
                    <span
                      :class="[
                        'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium',
                        getStatus(tunnel) === 'online' ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-600'
                      ]"
                    >
                      {{ getStatusText(tunnel) }}
                    </span>
                  </td>
                  <td class="text-sm">
                    {{ onlineMap.get(tunnel.tunnelId.toLowerCase())?.requestCount || 0 }}
                  </td>
                  <td>
                    <div class="flex gap-1.5">
                      <button
                        @click="editTunnel(tunnel)"
                        class="px-3 py-1.5 text-sm bg-gray-100 hover:bg-gray-200 rounded-md transition-colors"
                      >
                        编辑
                      </button>
                      <button
                        @click="deleteTunnel(tunnel.tunnelId)"
                        class="px-3 py-1.5 text-sm bg-red-50 text-red-600 hover:bg-red-100 rounded-md transition-colors"
                      >
                        删除
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </main>

    <!-- Toast -->
    <div
      v-if="showToast"
      class="fixed right-5 bottom-5 max-w-md bg-gray-900 text-white px-4 py-3 rounded-lg shadow-lg transition-opacity"
    >
      {{ toastMessage }}
    </div>
  </div>
</template>
