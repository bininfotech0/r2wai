import { PublicClientApplication, InteractionRequiredAuthError } from '@azure/msal-browser'
import type { IPublicClientApplication } from '@azure/msal-browser'

/**
 * Port of src/R2WAI.Web/wwwroot/js/auth.js. In Blazor these values came from
 * server-side IConfiguration; a client-rendered SPA has no server process to
 * read appsettings from, so they're baked in at build time instead (Vite env
 * vars, unset by default — matches today's default-disabled state, since
 * R2WAI.Web itself has no appsettings.json of its own and normally runs with
 * these unset too).
 */
const CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined
const TENANT_ID = import.meta.env.VITE_ENTRA_TENANT_ID as string | undefined

let msalInstance: IPublicClientApplication | null = null

export function isEntraConfigured(): boolean {
  return Boolean(CLIENT_ID && TENANT_ID)
}

export async function initMsal(): Promise<boolean> {
  if (!CLIENT_ID || !TENANT_ID) return false
  try {
    const instance = new PublicClientApplication({
      auth: {
        clientId: CLIENT_ID,
        authority: `https://login.microsoftonline.com/${TENANT_ID}`,
        redirectUri: window.location.origin,
      },
      cache: { cacheLocation: 'sessionStorage' },
    })
    await instance.initialize()
    msalInstance = instance
    return true
  } catch (err) {
    console.error('MSAL init failed:', err)
    return false
  }
}

export interface MsalLoginResult {
  success: boolean
  idToken?: string
  email?: string
  error?: string
}

export async function loginMicrosoft(): Promise<MsalLoginResult> {
  if (!msalInstance) return { success: false, error: 'MSAL not initialized' }
  try {
    const result = await msalInstance.loginPopup({
      scopes: ['openid', 'profile', 'email', 'User.Read'],
    })
    return { success: true, idToken: result.idToken, email: result.account?.username }
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      return { success: false, error: 'cancelled' }
    }
    const message = err instanceof Error ? err.message : 'Login failed'
    if (message.toLowerCase().includes('user_cancelled')) {
      return { success: false, error: 'cancelled' }
    }
    console.error('MSAL login error:', err)
    return { success: false, error: message }
  }
}
