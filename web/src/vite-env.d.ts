/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base da API (padrão: "/api", encaminhado pelo nginx/proxy do Vite). */
  readonly VITE_API_BASE_URL?: string
  /** URL do hub SignalR (padrão: "/hubs/orders"). */
  readonly VITE_HUB_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
