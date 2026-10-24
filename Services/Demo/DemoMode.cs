namespace Indlela.Services.Demo;

/// <summary>
/// Demo mode switches on the simulations (on-screen email verification, the simulated provider site,
/// demo courses and news). It is on only when "Demo:Enabled" is true in configuration, on your
/// computer and on the live site alike. Keep it false for real learners.
/// </summary>
public static class DemoMode
{
    public static bool IsOn(IConfiguration config, IWebHostEnvironment env) =>
        config.GetValue<bool>("Demo:Enabled");
}
