using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.OSC;

namespace VRCFaceTracking.Core.Services;

/**
 * OscSendService is responsible for encoding osc messages and sending them over OSC
 */
public class OscSendService : IDisposable
{
    private readonly ILogger<OscSendService> _logger;
    private readonly IOscTarget _oscTarget;

    private readonly Socket _sendSocket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private volatile IPEndPoint? _endpoint;
    private readonly byte[] _sendBuffer = new byte[4096];

    public Action<int> OnMessagesDispatched = _ => { };

    public OscSendService(
        ILogger<OscSendService> logger,
        IOscTarget oscTarget
    )
    {
        _logger = logger;

        _oscTarget = oscTarget;

        _oscTarget.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(IOscTarget.OutPort) or nameof(IOscTarget.OutputAddress))
                UpdateTarget();
        };
        UpdateTarget();
    }

    private void UpdateTarget()
    {
        _endpoint = _oscTarget.OutPort is > 0 and <= 65535 &&
            IPAddress.TryParse(_oscTarget.OutputAddress, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork
            ? new IPEndPoint(ip, _oscTarget.OutPort) : null;
    }

    public void Dispose() => _sendSocket.Dispose();

    public async Task Send(OscMessage message, CancellationToken ct)
    {
        var endpoint = _endpoint;
        if (endpoint == null) return;

        var nextByteIndex = await message.Encode(_sendBuffer, ct);
        if (nextByteIndex > 4096)
        {
            _logger.LogError("OSC message too large to send! Skipping this batch of messages.");
            return;
        }

        await _sendSocket.SendToAsync(_sendBuffer.AsMemory(0, nextByteIndex), SocketFlags.None, endpoint, ct);
        OnMessagesDispatched(1);
    }

    public async Task Send(IReadOnlyList<OscMessage> messages, CancellationToken ct)
    {
        var endpoint = _endpoint;
        if (endpoint == null) return;

        // Skip disposed / incomplete messages. Leaving a freed Value pointer with ValueLength > 0
        // (or a null Address) in the native bundle path causes an Access Violation in fti_osc.
        var cbt = messages
            .Where(static m =>
                m is { IsDisposed: false, Address: not null } &&
                !(m._meta.ValueLength > 0 && m._meta.Value == IntPtr.Zero))
            .Select(static m => m._meta)
            .ToArray();

        if (cbt.Length == 0)
        {
            return;
        }

        var index = 0;
        while (index < cbt.Length)
        {
            var length = await Task.Run(() => fti_osc.create_osc_bundle(_sendBuffer, cbt, cbt.Length, ref index), ct);
            await _sendSocket.SendToAsync(_sendBuffer.AsMemory(0, length), SocketFlags.None, endpoint, ct);
        }
        OnMessagesDispatched(index);
    }
}
