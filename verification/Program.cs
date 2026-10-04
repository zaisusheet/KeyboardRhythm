using System;
using KeyboardRhythm.SilentPrototype.Editor;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Console.WriteLine("Gameplay core checks passed: " + GameplayCoreChecks.Run());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Gameplay core checks FAILED: " + ex);
            return 1;
        }
    }
}
