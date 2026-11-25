// auth-guard.js
// Loaded as an ES module from AuthenticationGuard.razor.
//
// v1 used IJSRuntime.InvokeVoidAsync("eval", "<inline source>"). That fails under
// any strict CSP (script-src 'self' without 'unsafe-eval') and turns the page
// into an XSS amplifier if the script string ever interpolates user content.
// Promoting it to a real module makes it CSP-clean and easier to lint.

export function preventBackButtonAfterLogout() {
    if (!window.history || typeof window.history.pushState !== "function") {
        return;
    }
    window.history.pushState(null, "", window.location.href);
    window.addEventListener("popstate", function onPopState() {
        window.history.pushState(null, "", window.location.href);
        window.location.reload();
    }, { once: false });
}
