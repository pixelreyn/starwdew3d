using StardewModdingAPI;

namespace StardewValley3D.Structures;

public class MonitorHelper
{
    public IMonitor Monitor;
    private IModHelper _helper;
    
    public string DirectoryPath => _helper.DirectoryPath; 
    
    public IGameContentHelper GameContent => _helper.GameContent;
    public IModContentHelper ModContent => _helper.ModContent;
    
    public IInputHelper Input => _helper.Input;
    
    public MonitorHelper(IModHelper helper, IMonitor monitor)
    {
        Monitor = monitor;
        _helper = helper;
    }
}