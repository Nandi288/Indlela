namespace Indlela.Services.Demo;

/// <summary>
/// Demo mode switches on the simulations (on-screen email verification, the simulated provider site,
/// demo courses and news). It is always on in Development, and on a deployed site only when
/// "Demo:Enabled" is true in configuration. Turn it off before real learners use the site.
/// </summary>
public static class DemoMode
{
    public static bool IsOn(IConfiguration config, IWebHostEnvironment env) =>
        env.IsDevelopment() || config.GetValue<bool>("Demo:Enabled");
}
