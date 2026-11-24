using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Validation;

namespace VRCFaceTracking.Core.Models;

public partial class OscTarget : ObservableValidator, IOscTarget
{
    [ObservableProperty] private bool _isConnected;

    [ObservableProperty]
    [Range(1, 65535)]
    [property: SavedSetting("OSCInPort", 9001)]
    private int _inPort;

    [ObservableProperty]
    [Range(1, 65535)]
    [property: SavedSetting("OSCOutPort", 9000)]
    private int _outPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputAddress))]
    [ValidIpAddress]
    [property: SavedSetting("OSCAddress", "127.0.0.1")]
    private string _destinationAddress = "127.0.0.1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputAddress), nameof(OutputLabel))]
    [property: SavedSetting("OSCAutomaticOutput", true)]
    private bool _automaticOutput = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputAddress), nameof(OutputLabel))]
    private string? _streamingPeer;

    public string OutputAddress => AutomaticOutput ? StreamingPeer ?? "127.0.0.1" : DestinationAddress;
    public string OutputLabel => !AutomaticOutput ? "Manual destination"
        : StreamingPeer != null ? "Steam Link PC" : "This device";

    private readonly Lazy<Task> _loadSettings;
    public Task LoadSettingsAsync() => _loadSettings.Value;

    public OscTarget(ILocalSettingsService localSettingsService)
    {
        _loadSettings = new Lazy<Task>(async () =>
        {
            await localSettingsService.Load(this);
            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName is nameof(InPort) or nameof(OutPort) or nameof(DestinationAddress) or nameof(AutomaticOutput))
                    _ = localSettingsService.Save(this);
            };
        });
    }
}
