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

export interface ApiResponse {
  publicOrigin: string
  baseDomain: string
  forwardedHeadersEnabled: boolean
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
