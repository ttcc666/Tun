import type { UnifiedApiResponse, TunnelsData, UpsertTunnelRequest, AuthStatusResponse, LoginRequest, SetupRequest } from './types'

const getToken = () => localStorage.getItem('tun.managementToken') || 'dev-token'

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)

  // Only add token for non-auth endpoints
  if (!path.startsWith('/api/auth/')) {
    headers.set('X-Tun-Token', getToken())
  }

  if (options.body) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, {
    ...options,
    headers,
    credentials: 'same-origin'
  })

  if (!response.ok && response.status !== 200) {
    throw new Error(`请求失败: ${response.status}`)
  }

  const result: UnifiedApiResponse<T> = await response.json()

  if (result.code !== 200) {
    throw new Error(result.message || '操作失败')
  }

  return result.data as T
}

export const api = {
  getTunnels: () => request<TunnelsData>('/api/config/tunnels'),

  upsertTunnel: (data: UpsertTunnelRequest) =>
    request<void>('/api/config/tunnels', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  deleteTunnel: (tunnelId: string) =>
    request<void>(`/api/config/tunnels/${encodeURIComponent(tunnelId)}`, {
      method: 'DELETE',
    }),

  // Auth endpoints
  getAuthStatus: () => request<AuthStatusResponse>('/api/auth/status'),

  setup: (data: SetupRequest) =>
    request<void>('/api/auth/setup', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  login: (data: LoginRequest) =>
    request<void>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  logout: () =>
    request<void>('/api/auth/logout', {
      method: 'POST',
    }),
}

export const tokenStorage = {
  get: getToken,
  set: (token: string) => localStorage.setItem('tun.managementToken', token),
}
