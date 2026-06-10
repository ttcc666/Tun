export interface TunnelConfig {
  tunnelId: string
  clientId: string
  localUrl: string
  enabled: boolean
  description: string
  createdAt: string
  updatedAt: string
  publicUrl: string
}

export interface TunnelOnlineStatus {
  tunnelId: string
  requestCount: number
}

export interface UnifiedApiResponse<T> {
  code: number
  message: string
  data: T | null
}

export interface TunnelsData {
  publicOrigin: string
  baseDomain: string
  configured: TunnelConfig[]
  online: TunnelOnlineStatus[]
}

export interface UpsertTunnelRequest {
  tunnelId: string
  clientId: string
  localUrl: string
  enabled: boolean
  description: string
}

export interface AuthStatusResponse {
  isInitialized: boolean
  isAuthenticated: boolean
}

export interface LoginRequest {
  password: string
}

export interface SetupRequest {
  password: string
}
