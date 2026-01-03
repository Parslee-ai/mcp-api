import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';

const api = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001/api',
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

let accessToken: string | null = null;
let refreshPromise: Promise<string | null> | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

async function refreshAccessToken(): Promise<string | null> {
  try {
    const response = await axios.post(
      `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001/api'}/auth/refresh`,
      {},
      { withCredentials: true }
    );
    const newToken = response.data.accessToken;
    setAccessToken(newToken);
    return newToken;
  } catch {
    setAccessToken(null);
    return null;
  }
}

// Request interceptor - add auth header
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

// Extended request config to track retry state
interface ExtendedAxiosRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

// Response interceptor - handle 401 and refresh token
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as ExtendedAxiosRequestConfig | undefined;

    if (error.response?.status === 401 && originalRequest) {
      // Avoid infinite loops
      if (originalRequest._retry) {
        setAccessToken(null);
        // Only redirect if not already on auth pages or landing page
        if (typeof window !== 'undefined' &&
            !window.location.pathname.startsWith('/auth') &&
            window.location.pathname !== '/') {
          window.location.href = '/auth/login';
        }
        return Promise.reject(error);
      }

      originalRequest._retry = true;

      // Use singleton promise to prevent multiple refresh calls
      if (!refreshPromise) {
        refreshPromise = refreshAccessToken().finally(() => {
          refreshPromise = null;
        });
      }

      const newToken = await refreshPromise;
      if (newToken) {
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return api(originalRequest);
      }

      // Only redirect if not already on auth pages or landing page
      if (typeof window !== 'undefined' &&
          !window.location.pathname.startsWith('/auth') &&
          window.location.pathname !== '/') {
        window.location.href = '/auth/login';
      }
    }

    return Promise.reject(error);
  }
);

export default api;

// Auth API
export const authApi = {
  // Get available OAuth providers
  getProviders: () => api.get<{ providers: string[] }>('/auth/providers'),

  // Get GitHub OAuth login URL for redirecting
  getGitHubLoginUrl: (returnUrl?: string) => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001/api';
    const params = returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '';
    return `${baseUrl}/auth/login/github${params}`;
  },

  logout: () => api.post('/auth/logout'),

  refresh: () => api.post<{ accessToken: string; user: User }>('/auth/refresh'),

  getMe: () => api.get<User>('/auth/me'),
};

// APIs API
export const apisApi = {
  getAll: () => api.get<ApiRegistration[]>('/apis'),

  get: (id: string) => api.get<ApiDetail>(`/apis/${id}`),

  register: (data: {
    name: string;
    specUrl: string;
    description?: string;
    authType: string;
    apiKey?: string;
    apiKeyHeader?: string;
    bearerToken?: string;
    basicUsername?: string;
    basicPassword?: string;
  }) => api.post<ApiRegistration>('/apis', data),

  update: (id: string, data: { displayName?: string; isEnabled?: boolean }) =>
    api.put<ApiRegistration>(`/apis/${id}`, data),

  delete: (id: string) => api.delete(`/apis/${id}`),

  toggle: (id: string, enabled: boolean) =>
    api.put<ApiRegistration>(`/apis/${id}/toggle`, { enabled }),

  refresh: (id: string) => api.post<ApiDetail>(`/apis/${id}/refresh`),

  getEndpoints: (id: string) => api.get<ApiEndpoint[]>(`/apis/${id}/endpoints`),

  toggleEndpoint: (apiId: string, endpointId: string, enabled: boolean) =>
    api.put<ApiEndpoint>(`/apis/${apiId}/endpoints/${endpointId}/toggle`, { enabled }),
};

// Tokens API
export const tokensApi = {
  getAll: () => api.get<McpToken[]>('/tokens'),

  create: (name: string, expiresAt?: string) =>
    api.post<{ token: McpToken; plaintextToken: string }>('/tokens', { name, expiresAt }),

  revoke: (id: string) => api.put(`/tokens/${id}/revoke`),

  delete: (id: string) => api.delete(`/tokens/${id}`),
};

// Usage API
export const usageApi = {
  getSummary: () => api.get<UsageSummary>('/usage/summary'),

  getHistory: (months?: number) =>
    api.get<UsageRecord[]>(`/usage/history${months ? `?months=${months}` : ''}`),
};

// Types
export interface User {
  id: string;
  email: string;
  displayName?: string;
  avatarUrl?: string;
  oAuthProvider?: string;
  emailVerified: boolean;
  tier: string;
  createdAt: string;
}

export interface ApiRegistration {
  id: string;
  displayName: string;
  baseUrl: string;
  specUrl?: string;
  openApiVersion: string;
  apiVersion?: string;
  description?: string;
  authType: string;
  isEnabled: boolean;
  createdAt: string;
  lastRefreshed: string;
  endpointCount: number;
  enabledEndpointCount: number;
}

export interface ApiEndpoint {
  id: string;
  apiId: string;
  operationId: string;
  method: string;
  path: string;
  summary?: string;
  description?: string;
  tags: string[];
  isEnabled: boolean;
}

export interface ApiDetail extends Omit<ApiRegistration, 'endpointCount' | 'enabledEndpointCount'> {
  auth: {
    authType: string;
    name?: string;
    in?: string;
    parameterName?: string;
    prefix?: string;
  };
  endpoints: ApiEndpoint[];
}

export interface McpToken {
  id: string;
  name: string;
  createdAt: string;
  lastUsedAt?: string;
  expiresAt?: string;
  isRevoked: boolean;
  isActive: boolean;
}

export interface UsageSummary {
  apiCallsUsed: number;
  apiCallsLimit: number;
  apiCallsRemaining: number;
  apisRegistered: number;
  apisLimit: number;
  apisRemaining: number;
  tier: string;
  yearMonth: string;
}

export interface UsageRecord {
  yearMonth: string;
  apiCallCount: number;
  firstCallAt?: string;
  lastCallAt?: string;
}
