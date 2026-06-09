import type { ApiResponse, UpsertTunnelRequest, TunnelConfig } from './types'

const getToken = () => localStorage.getItem('tun.managementToken') || 'dev-token'

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('X-Tun-Token', getToken())
  if (options.body) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, { ...options, headers })

  if (response.status === 401) {
    throw new Error('Token 无效或未提供')
  }

  if (!response.ok) {
    let message = `请求失败: ${response.status}`
    try {
      const problem = await response.json()
      message = problem.error || message
    } catch {
      // ignore
    }
    throw new Error(message)
  }

  if (response.status === 204) {
    return null as T
  }

  return response.json()
}

export const api = {
  getTunnels: () => request<ApiResponse>('/api/config/tunnels'),

  upsertTunnel: (data: UpsertTunnelRequest) =>
    request<TunnelConfig>('/api/config/tunnels', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  deleteTunnel: (tunnelId: string) =>
    request<void>(`/api/config/tunnels/${encodeURIComponent(tunnelId)}`, {
      method: 'DELETE',
    }),
}

export const tokenStorage = {
  get: getToken,
  set: (token: string) => localStorage.setItem('tun.managementToken', token),
}
