using System.ComponentModel;

namespace VRCFaceTracking.Core.Contracts;

public interface IOscTarget : INotifyPropertyChanged
{
    public bool IsConnected
    {
        get;
        set;
    }
    
    public int InPort
    {
        get;
        set;
    }
    
    public int OutPort
    {
        get;
        set;
    }
    
    Task LoadSettingsAsync();
    bool AutomaticOutput { get; set; }
    string? StreamingPeer { get; set; }
    string OutputAddress { get; }
    string OutputLabel { get; }

    public string DestinationAddress
    {
        get;
        set;
    }
}