let _msalInstance = null;

export async function initMsal(clientId, tenantId) {
    if (!clientId || !tenantId) return false;
    try {
        const config = {
            auth: {
                clientId,
                authority: `https://login.microsoftonline.com/${tenantId}`,
                redirectUri: window.location.origin,
            },
            cache: { cacheLocation: 'sessionStorage', storeAuthStateInCookie: false }
        };
        _msalInstance = new msal.PublicClientApplication(config);
        await _msalInstance.initialize();
        return true;
    } catch (e) {
        console.error('MSAL init failed:', e);
        return false;
    }
}

export async function loginMicrosoft() {
    if (!_msalInstance) return { success: false, error: 'MSAL not initialized' };
    try {
        const result = await _msalInstance.loginPopup({
            scopes: ['openid', 'profile', 'email', 'User.Read']
        });
        return { success: true, idToken: result.idToken, email: result.account.username };
    } catch (e) {
        if (e.errorCode === 'user_cancelled') return { success: false, error: 'cancelled' };
        console.error('MSAL login error:', e);
        return { success: false, error: e.message ?? 'Login failed' };
    }
}
