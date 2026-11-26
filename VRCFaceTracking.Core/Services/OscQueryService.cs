using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.mDNS;
using VRCFaceTracking.Core.Models.ParameterDefinition;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.OSC.Query.mDNS;
using VRCFaceTracking.Core.Params;
using VRCFaceTracking.Core.Params.Data.Mutation;

namespace VRCFaceTracking.Core.Services;

public partial class OscQueryService(
    ILogger<OscQueryService> logger,
    OscQueryConfigParser oscQueryConfigParser,
    AvatarConfigParser avatarConfigParser,
    ParameterSenderService parameterSenderService,
    IOscTarget oscTarget,
    MulticastDnsService multicastDnsService,
    OscRecvService recvService,
    HttpHandler httpHandler,
    IHostApplicationLifetime lifetime,
    IDispatcherService dispatcherService)
    : ObservableObject
{
    [ObservableProperty] private IAvatarInfo _avatarInfo = new NullAvatarDef("Loading...", "Loading...");
    [ObservableProperty] private List<Parameter> _avatarParameters;

    // Avatar parsing mutates shared OSC parameter state and the send queue. Concurrent parses
    // (FirstClientDiscovered + OnHostInfoQueried) can dispose messages that are already queued.
    private readonly SemaphoreSlim _avatarParseLock = new(1, 1);
    private int _avatarParseGeneration;
    private int _clientInitialized;

    public async Task InitializeAsync()
    {
        logger.LogDebug("OSC Service Initializing");

        recvService.OnMessageReceived = HandleNewMessage;

        await oscTarget.LoadSettingsAsync();

        multicastDnsService.OnVrcClientDiscovered += FirstClientDiscovered;

        multicastDnsService.SendQuery("_oscjson._tcp.local");

        if (multicastDnsService.VrchatClientEndpoint != null)
            FirstClientDiscovered();

        if (OperatingSystem.IsLinux())
            _ = DiscoverLocalProtonClientAsync(lifetime.ApplicationStopping);
    }

    private async Task DiscoverLocalProtonClientAsync(CancellationToken token)
    {
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(token) && multicastDnsService.VrchatClientEndpoint == null)
            {
                try
                {
                    var directory = Path.GetDirectoryName(VRChat.VRCOSCDirectory);
                    if (!Directory.Exists(directory)) continue;
                    var log = Directory.EnumerateFiles(directory, "output_log_*.txt")
                        .MaxBy(Path.GetFileName, StringComparer.Ordinal);
                    if (log == null) continue;

                    var advertisement = File.ReadLines(log)
                        .Select(line => Regex.Match(line, @"Advertising Service (VRChat-Client-[\w-]+) of type OSCQuery on (\d+)\s*$"))
                        .FirstOrDefault(match => match.Success);
                    if (advertisement == null || !ushort.TryParse(advertisement.Groups[2].Value, out var port) || port == 0)
                        continue;

                    var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                    using var info = JsonDocument.Parse(await http.GetStringAsync($"http://{endpoint}/?HOST_INFO", token));
                    if (!info.RootElement.TryGetProperty("NAME", out var name) ||
                        !name.ValueEquals(advertisement.Groups[1].Value) || multicastDnsService.VrchatClientEndpoint != null)
                        continue;

                    logger.LogInformation("Found local VRChat OSCQuery endpoint in Proton log at {Endpoint}", endpoint);
                    multicastDnsService.VrchatClientEndpoint = endpoint;
                    FirstClientDiscovered();
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException
                    or JsonException or InvalidOperationException or TaskCanceledException)
                {
                    logger.LogDebug("Local VRChat OSCQuery endpoint unavailable: {Reason}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void FirstClientDiscovered()
    {
        if (Interlocked.Exchange(ref _clientInitialized, 1) != 0) return;
        multicastDnsService.OnVrcClientDiscovered -= FirstClientDiscovered;

        logger.LogInformation($"OSCQuery detected at {multicastDnsService.VrchatClientEndpoint}. Setting port negotiation to autopilot.");

        httpHandler.OnHostInfoQueried += HandleNewAvatarWrapper;

        var recvEndpoint = recvService.UpdateTarget(new IPEndPoint(IPAddress.Loopback, 0));
        if (recvEndpoint == null)
        {
            logger.LogError("Very strange. We were unable to bind to a random port.");
            recvEndpoint = new IPEndPoint(IPAddress.Loopback, oscTarget.InPort);
        }

        var randomServiceSuffix = Utils.GetRandomChars(6);
        var httpPort = Utils.GetRandomFreePort();
        httpHandler.SetAppName("VRCFT-" + randomServiceSuffix);
        httpHandler.BindTo($"http://127.0.0.1:{httpPort}/", recvEndpoint.Port);

        // Advertise our OSC JSON and OSC endpoints (OSC JSON to display the silly lil popup in-game)
        multicastDnsService.Advertise("_oscjson._tcp", new AdvertisedService("VRCFT-"+randomServiceSuffix, httpPort, IPAddress.Loopback));
        multicastDnsService.Advertise("_osc._udp", new AdvertisedService("VRCFT-"+randomServiceSuffix, recvEndpoint.Port, IPAddress.Loopback));

        HandleNewAvatar();
    }

    private async void HandleNewAvatar(string newId = null)
    {
        var generation = Interlocked.Increment(ref _avatarParseGeneration);
        await _avatarParseLock.WaitAsync();
        try
        {
            // A newer avatar-change request arrived while we were waiting; drop this one.
            if (generation != Volatile.Read(ref _avatarParseGeneration))
            {
                return;
            }

            (IAvatarInfo avatarInfo, List<Parameter> relevantParameters)? newAvatar;
            if (multicastDnsService.VrchatClientEndpoint != null)
            {
                newAvatar = await oscQueryConfigParser.ParseAvatar("");
            }
            else
            {
                // handle normal osc
                newAvatar = await avatarConfigParser.ParseAvatar(newId);
            }

            if (!newAvatar.HasValue)
            {
                return;
            }

            // Parsing success. Deregister callback and update values
            httpHandler.OnHostInfoQueried -= HandleNewAvatarWrapper;
            dispatcherService.Run(() =>
            {
                AvatarInfo = newAvatar.Value.avatarInfo;
                AvatarParameters = newAvatar.Value.relevantParameters;
            });
        }
        finally
        {
            _avatarParseLock.Release();
        }
    }

    private void HandleNewAvatarWrapper() => HandleNewAvatar(); // Helper func used in callbacks
    

    private void HandleNewMessage(OscMessage msg)
    {
        switch (msg.Address)
        {
            case "/avatar/change":
                HandleNewAvatar(msg.Value as string);
                break;
            case "/avatar/parameters/AFK":
                if (msg.Value is bool afk)
                {
                    DisableOnAfk.IsAfk = afk;
                }
                break;
            case "/vrcft/settings/forceRelevant":   // Endpoint for external tools to force vrcft to send all parameters
                if (msg.Value is bool relevancy)
                {
                    parameterSenderService.AllParametersRelevant = relevancy;
                }

                break;
            /*
            case "/avatar/parameters/EyeTrackingActive":
                if (UnifiedLibManager.EyeStatus != ModuleState.Uninitialized)
                {
                    if (!msg.Value.BoolValue)
                        UnifiedLibManager.EyeStatus = ModuleState.Idle;
                    else UnifiedLibManager.EyeStatus = ModuleState.Active;
                }
                break;
            case "/avatar/parameters/LipTrackingActive":
            case "/avatar/parameters/ExpressionTrackingActive":
                {
                    if (!msg.Value.BoolValue)
                if (UnifiedLibManager.ExpressionStatus != ModuleState.Uninitialized)
                        UnifiedLibManager.ExpressionStatus = ModuleState.Idle;
                    else UnifiedLibManager.ExpressionStatus = ModuleState.Active;
                }
                break;
            */
        }
    }

    partial void OnAvatarParametersChanged(List<Parameter> value)
    {
        logger.LogDebug("Configuration loaded. Checking for native tracking parameters...");
        var hasLoadedNative = value.Any(p => p.GetParamNames().Any(t => t.paramName.StartsWith("/tracking/")));
        if (hasLoadedNative)
        {
            logger.LogWarning("Native tracking parameters detected.");
        }

        var deprecatedParams = value.Where(p => p.Deprecated).ToList();

        logger.LogInformation(value.Count + " parameters loaded.");
        if (deprecatedParams.Any())
        {
            logger.LogWarning(
                deprecatedParams.Count +
                " Legacy parameters detected. " +
                "Please consider updating the avatar to use the latest documented parameters.");
            logger.LogDebug($"Loaded deprecated parameters: {string.Join(", ", deprecatedParams.SelectMany(x => x.GetParamNames()).Select(y => y.paramName))}");
        }
    }
}
