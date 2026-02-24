using MSCLoader;

namespace I386API;

internal class I386API : Mod {
    public override string ID => "I386API";
    public override string Name => "I386 API";
    public override string Author => "tommojphillips";
    public override string Version => "1.2";
    public override string Description => "I386 API";
    public override Game SupportedGames => Game.MyWinterCar;

    public override void ModSetup() {
        SetupFunction(Setup.PreLoad, Mod_OnPreLoad);
    }

    private void Mod_OnPreLoad() {
        I386.i386 = new I386();
    }
}
