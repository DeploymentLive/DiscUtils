using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Streams;
using DiscUtils.Fat;

namespace floppytest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            File.Delete("C:\\Users\\keith\\source\\repos\\iPXEBuild\\Build\\Block\\test3.vfd");
            FileStream ms = new FileStream("C:\\Users\\keith\\source\\repos\\iPXEBuild\\Build\\Block\\test3.vfd", FileMode.CreateNew);
            using (FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, "iPXE"))
            {

                fs.CreateDirectory("EFI\\BOOT");

                using (Stream s = fs.OpenFile("EFI\\BOOT\\BOOTX64.EFI", FileMode.CreateNew))
                {
                    FileStream rs = File.OpenRead("C:\\Users\\keith\\source\\repos\\iPXEBuild\\Build\\Signed\\snp_drv_x64.efi");
                    rs.CopyTo(s);
                    rs.Close();
                }


                using (Stream s = fs.OpenFile("autoexec.ipxe", FileMode.Create))
                {
                    MemoryStream autoexec = new MemoryStream(Encoding.UTF8.GetBytes("#!ipxe\r\nset force_filename https://boot.deploymentlive.com:8050/boot/cloudboot.ipxe\r\n"));
                    autoexec.WriteTo(s);
                    autoexec.Close();
                }

            }

            ms.Close();

        }
    }
}
