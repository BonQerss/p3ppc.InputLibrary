using System.ComponentModel;
using p3ppc.InputLibrary.Template.Configuration;
using Reloaded.Mod.Interfaces.Structs;
using System.ComponentModel.DataAnnotations;

using Reloaded.Mod.Interfaces;
using System;

namespace p3ppc.InputLibrary.Configuration;

public class Config : Configurable<Config>
{
    // /*
    //     User Properties:
    //         - Please put all of your configurable properties here.

    //     By default, configuration saves as "Config.json" in mod user config folder.    
    //     Need more config files/classes? See Configuration.cs

    //     Available Attributes:
    //     - Category
    //     - DisplayName
    //     - Description
    //     - DefaultValue

    //     // Technically Supported but not Useful
    //     - Browsable
    //     - Localizable

    //     The `DefaultValue` attribute is used as part of the `Reset` button in Reloaded-Launcher.
    // */

    [DisplayName("Enable Debug Logging")]
    [Description("Writes detailed input and hook information to the Reloaded-II console.")]
    public bool DebugEnabled { get; set; } = false;
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    //
}
