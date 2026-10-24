using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaterWatch.Security;

namespace WaterWatch.Realtime;

[Authorize(Policy = Policies.CanViewTelemetry)]
public class TelemetryHub : Hub
{
    public Task SubscribeToDevice(string deviceCode) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceCode}");

    public Task UnsubscribeFromDevice(string deviceCode) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceCode}");
}
