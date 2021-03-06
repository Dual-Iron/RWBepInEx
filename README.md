Rain World BepInEx file soup. Works with BepInEx plugins, patchers, and Partiality mods. 

# THIS IS WIP

<details>
  <summary>Credits</summary>
  
* Pastebee, for help with some debugging and their Dragons code samples
  * Discord: ID is 695651562542071871
  * GitHub: https://github.com/aaay-aaay/
* The Rain World community, for making this worthwhile
  * Discord: https://discord.gg/rainworld
* The BepInEx team
  * GitHub: https://github.com/BepInEx/
</details>

# To install
### Method 1: From fresh Rain World install
1. Unzip the provided zip file from [the latest release](https://github.com/Dual-Iron/RainyBepis/releases/latest) somewhere safe.
2. Put the contents of the zip file into your Rain World directory. (By default: `C:\Program Files (x86)\Steam\steamapps\common\Rain World`.)
3. Run [thalber's BlepOutIn](https://github.com/thalber/BOI/) like usual.

[This video](https://github.com/Dual-Iron/RainyBepis/raw/main/bix%20install.mp4) details installing bix, and [this video](https://github.com/Dual-Iron/RainyBepis/raw/main/mod%20install.mp4) details installing a mod ([thalber's BetterShelters](https://github.com/thalber/BetterShelters)).

### Method 2: From pastebee's 5.0 BepInEx
Make sure BlepInOut is closed while following these instructions.
1. In the Rain World directory, delete: `BepInEx/`, `doorstop_config.ini`, `winhttp.dll` (and `Mods/` if present)
2. [Goto method 1](#from-fresh-rain-world-install)

If it seems like mods are being initialized & enabled twice, it's because they are. That's a sign that you didn't delete the old `BepInEx/` folder; make sure you do!

### Method 3: From Partiality
1. Uninstall partiality completely. (TODO describe this better lol)
2. [Goto method 1](#from-fresh-rain-world-install)

# To see console logs
This is very useful for debugging. Console logs show all BepInEx and Unity logs in real time.
1. Open `BepInEx\config\BepInEx.cfg`.
2. Find `[Logging.Console]`.
3. Change `Enabled = false` to `Enabled = true`.

There are also other logging options in the .cfg file that can be safely modified. Tune to your liking.

# To use [ScriptEngine](https://github.com/BepInEx/BepInEx.Debug#scriptengine)
ScriptEngine allows you to hot reload mods, i.e., while the game is running, and at any time. Needless to say, this is absolutely invaluable to debugging.

To install:

1. Download the ScriptEngine zip from [the latest release](https://github.com/BepInEx/BepInEx.Debug/releases/latest).
2. Put the contained DLL file into `BepInEx\plugins`.
3. Create the folder `BepInEx\scripts`.

To use:

1. Put new DLL files you want to hot reload into `BepInEx\scripts`. These can be taken right from your mod's output immediately!
2. Press F6 in-game.

That's it! ScriptEngine will log what mods it reloaded.

<details>
  <summary>FOR DEVELOPERS: Important note regarding hot reloading</summary>
   
Anything you did to other assemblies will remain after reloading the plugin. So, if you subscribe to a MonoMod hook in your plugin's `BaseUnityPlugin.OnEnable()` method, make sure to unsubscribe to it in `BaseUnityPlugin.OnDisable()`. Example: 
```cs
// class Plugin : BaseUnityPlugin
public void OnEnable()
{
    On.Player.Update += Player_Update;
}
public void OnDisable()
{
    On.Player.Update -= Player_Update;
}
private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
{
    orig(self, eu);
    this.Logger.LogInfo("Hello world!");
}
```

This ensures that everything is undone after unloading your `BaseUnityPlugin`.

</details>

I also suggest checking out [this guide](https://github.com/risk-of-thunder/R2Wiki/wiki/Debugging-Your-Mods-With-dnSpy) about debugging mods with dnSpy.
