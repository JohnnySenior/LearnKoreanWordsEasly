using Lugat.Contracts;
using Microsoft.JSInterop;

namespace Lugat.Web.Services;

/// <summary>
/// Web Push on this device (L-63), shared by Profil → Eslatma and the offer after a passed unit test: what the device
/// can do (js/push.js state()), turning it on from a tap, and off.
/// </summary>
public sealed class PushDevice(IJSRuntime js, SessionService api) : IAsyncDisposable
{
    /// <summary>"Keyinroq" on the offer: it does not come back on this device.</summary>
    public const string OfferDismissedKey = "lugat.push.offer";

    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> ModuleAsync() => _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/push.js");

    /// <summary>install, unsupported, denied, ready or subscribed.</summary>
    public async Task<string> StateAsync()
    {
        try
        {
            return await (await ModuleAsync()).InvokeAsync<string>("state");
        }
        catch (JSException)
        {
            return "unsupported";
        }
    }

    /// <summary>Asks for permission and saves the subscription. Returns a message for the learner (null when "denied": the page explains).</summary>
    public async Task<(bool Ok, string? Message)> EnableAsync()
    {
        var key = await api.GetAsync<PushKeyDto>("api/me/push/key");
        if (key.Value is not { } k)
        {
            return (false, key.Error ?? "Bildirishnomalar hozircha serverda yoqilmagan.");
        }

        var sub = await (await ModuleAsync()).InvokeAsync<DeviceSubscription>("subscribe", k.PublicKey);
        if (sub is { Endpoint: { } endpoint, P256dh: { } p256dh, Auth: { } auth })
        {
            var saved = await api.SendJsonAsync<object>(HttpMethod.Post, "api/me/push", new PushSubscribeRequest(endpoint, p256dh, auth));
            return saved.Ok ? (true, "Shu qurilmaga eslatma keladi.") : (false, saved.Error);
        }

        return (false, sub?.Error switch
        {
            "denied" => null,
            "dismissed" => "Ruxsat berilmadi. Xohlasangiz, qayta yoqib ko‘ring.",
            _ => "Bildirishnomani yoqib bo‘lmadi. Birozdan keyin qayta urinib ko‘ring.",
        });
    }

    public async Task<string?> DisableAsync()
    {
        if (await (await ModuleAsync()).InvokeAsync<string?>("unsubscribe") is not { } endpoint)
        {
            return null;
        }

        await api.SendJsonAsync<object>(HttpMethod.Delete, "api/me/push", new PushUnsubscribeRequest(endpoint));
        return "Shu qurilmaga eslatma kelmaydi.";
    }

    private sealed record DeviceSubscription(string? Endpoint, string? P256dh, string? Auth, string? Error);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        }
    }
}
