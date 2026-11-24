using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using VRCFaceTracking.Core.Contracts;

namespace VRCFaceTracking.Core.Services;

internal sealed class SteamLinkOutputDiscovery(IOscTarget target) : BackgroundService
{
    private readonly FileInfo _log = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam/logs/vrlink.txt"));
    private (DateTime Modified, long Size)? _version;
    private int _pid;
    private string? _peer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        do
        {
            target.StreamingPeer = ReadLivePeer();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private string? ReadLivePeer()
    {
        try
        {
            _log.Refresh();
            if (!_log.Exists)
            {
                _version = null;
                return null;
            }

            var version = (_log.LastWriteTimeUtc, _log.Length);
            if (_version != version)
            {
                (_pid, _peer) = ParseSession(File.ReadLines(_log.FullName));
                _version = version;
            }

            if (_peer == null || _pid <= 0) return null;
            var executable = File.ReadAllText($"/proc/{_pid}/cmdline").Split('\0', 2)[0];
            return Path.GetFileName(executable).Contains("vrlink", StringComparison.Ordinal) ? _peer : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static (int Pid, string? Address) ParseSession(IEnumerable<string> lines)
    {
        int pid = 0;
        string? selected = null, peer = null;
        bool inList = false;
        foreach (var line in lines)
        {
            int prefix = line.IndexOf("] - ", StringComparison.Ordinal);
            var message = prefix < 0 ? line : line[(prefix + 4)..];
            if (message.Contains("startup with PID=", StringComparison.Ordinal))
            {
                int.TryParse(Regex.Match(message, @"startup with PID=(\d+)").Groups[1].Value, out pid);
                selected = peer = null;
                inList = false;
            }
            else if (message.Contains("UDPServerAddresses:", StringComparison.Ordinal))
            {
                inList = true;
                selected = null;
            }
            else if (inList && message.StartsWith('\t'))
            {
                if (message.Contains('*')) selected = ReadAddress(message);
            }
            else
            {
                inList = false;
                int arrow = message.IndexOf("-> ", StringComparison.Ordinal);
                if (arrow >= 0 && message.Contains("SetPeer ", StringComparison.Ordinal))
                    peer = ReadAddress(message[(arrow + 3)..]);
            }
        }
        return (pid, selected ?? peer);
    }

    private static string? ReadAddress(string text)
    {
        var token = Regex.Match(text, @"[\d.]+").Value;
        return token.Count(c => c == '.') == 3 && IPAddress.TryParse(token, out var address)
            && address.AddressFamily == AddressFamily.InterNetwork ? address.ToString() : null;
    }
}
