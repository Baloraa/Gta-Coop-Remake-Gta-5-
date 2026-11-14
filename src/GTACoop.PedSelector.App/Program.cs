using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GTACoop.PedSelector.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // CLI: GTACoop.PedSelector.exe --build-oiv <pedKlasörü> <çıktı.oiv>
            if (args.Length >= 3 &&
                args[0].Equals("--build-oiv", StringComparison.OrdinalIgnoreCase))
            {
                var packDir = Path.GetFullPath(args[1]);
                var output = Path.GetFullPath(args[2]);
                try
                {
                    if (!Directory.Exists(packDir))
                    {
                        Console.Error.WriteLine("Klasör yok: " + packDir);
                        return 2;
                    }

                    var path = PedOiv.Build(packDir, output, message => Console.WriteLine(message));
                    var size = new FileInfo(path).Length;
                    Console.WriteLine($"TAMAM: {path} ({size / 1024} KB)");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("HATA: " + ex.Message);
                    return 1;
                }
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new PedSelectorForm());
            return 0;
        }
    }
}
