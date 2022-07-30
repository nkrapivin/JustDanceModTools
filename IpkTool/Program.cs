using System.Diagnostics;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using System;

namespace Nik
{
    public static class Program
    {
        private static async Task LoggerConsole(string thing) =>
            await Console.Out.WriteLineAsync(thing);

        public static async Task UnpackIpk(string inputipk, string? outfolderpath, bool beverbose)
        {
            var input = inputipk;
            var output = outfolderpath;

            if (output is null || string.IsNullOrWhiteSpace(output))
            {
                await LoggerConsole("No output path specified.");
                output = Path.ChangeExtension(input, null);
            }

            await LoggerConsole($"Input: {input}\nOutput: {output}");

            await LoggerConsole("Trying to unpack an ipk...");

            using var fs = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var ipk = new IpkFile();
            if (beverbose) ipk.Logger = LoggerConsole;
            await ipk.ReadFromStream(fs);
            await fs.DisposeAsync();
            await ipk.WriteToDirectory(output);
        }

        public static async Task PackIpk(string inputfolder, string? outipkpath, bool beverbose)
        {
            var input = inputfolder;
            var output = outipkpath;

            if (output is null || string.IsNullOrWhiteSpace(output))
            {
                output = Path.ChangeExtension(input, ".ipk");
            }

            await LoggerConsole($"Input: {input}\nOutput: {output}");

            await LoggerConsole("Trying to cook an ipk...");

            using var fs = new FileStream(output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 4096, true);
            var ipk = new IpkFile();
            if (beverbose) ipk.Logger = LoggerConsole;
            await ipk.ReadFromDirectory(input);
            await ipk.Write(fs);
            await fs.DisposeAsync();
        }

        public static async Task ApplyPatchipk(string inputipk, string? outputipk, string inputpatchipk, bool beverbose)
        {
            var output = outputipk;
            if (output == null)
            {
                output = Path.ChangeExtension(inputpatchipk, ".ipk") ?? throw new ArgumentException("screw it");
            }

            await LoggerConsole($"Input: {inputipk}\nPatchipk: {inputpatchipk}\nOutput: {output}");

            var inpipk = new IpkFile(); if (beverbose) inpipk.Logger = LoggerConsole;
            using var fs = new FileStream(inputipk, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await inpipk.ReadFromStream(fs);
            await fs.DisposeAsync();

            var pipk = new IpkFile(); if (beverbose) pipk.Logger = LoggerConsole;
            using var pfs = new FileStream(inputpatchipk, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await pipk.ReadFromStream(pfs);
            await pfs.DisposeAsync();

            await inpipk.PatchFrom(pipk);

            using var ofs = new FileStream(output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 4096, true);
            await inpipk.Write(ofs);
            await ofs.DisposeAsync();
        }

        public static async Task MakePatchipk(string inputfolder, string? outputpatchipk, string originalipk, bool beverbose)
        {
            var output = outputpatchipk;
            if (output == null)
            {
                output = Path.ChangeExtension(originalipk, ".patchipk") ?? throw new ArgumentException("screw it");
            }

            await LoggerConsole($"Input: {inputfolder}\nOriginal ipk: {originalipk}\nOutput: {output}");

            var fromfolderipk = new IpkFile(); if (beverbose) fromfolderipk.Logger = LoggerConsole;
            await fromfolderipk.ReadFromDirectory(inputfolder);

            var fileipk = new IpkFile(); if (beverbose) fileipk.Logger = LoggerConsole;

            using var fs = new FileStream(originalipk, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await fileipk.ReadFromStream(fs);
            await fs.DisposeAsync();

            var patchipk = await fileipk.DiffWith(fromfolderipk); if (beverbose) patchipk.Logger = LoggerConsole;

            using var ofs = new FileStream(output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 4096, true);
            await patchipk.Write(ofs);
            await ofs.DisposeAsync();
        }

        public static async Task<int> IpkToolMain(string[] args)
        {
            try
            {
                // -- fix output and input on older Windows -- //
                // please always add these lines into your app if you can
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
                System.Console.InputEncoding = System.Text.Encoding.UTF8;
                // -- fix output and input on older Windows -- //

                var inputOption = new Option<string?>("--input", "A path to .ipk if unpacking or patching, to a folder if packing or making a patch");
                var outputOption = new Option<string?>("--output", "A path to a folder if unpacking, to an .ipk if packing");
                var verboseOption = new Option<bool>("--verbose", "[optional] Be verbose in console output");
                var patchipkOption = new Option<string?>("--patchipk", "A path to the original ipk when making a patch, and to patchipk if applying");
                var rootOption = new RootCommand(
                    "IpkTool - unpack, repack and patch .ipk files from Just Dance 2016+\n" +
                    "by nkrapivindev and Zhilemann@VULKAN.SYSTEMS (aka Ekspert Po Vsem Delam, Master Po Vsem Voprosam)\n" +
                    "The ipk format is proprietary and is a property of Ubisoft, ALWAYS MAKE BACKUPS OF YOUR WORK!\n\n\n" +

                    "Unpack an ipk (input is a file): ipktool --input bundle_nx.ipk\n" +
                    "Will unpack bundle_nx.ipk into the bundle_nx folder\n\n" +

                    "Package an ipk (input is a folder): ipktool --input bundle_nx\n" +
                    "Will package the bundle_nx folder into a bundle_nx.ipk file\n\n" +

                    "Make a patchipk: ipktool --input bundle_nx --patchipk bundle_nx.ipk\n" +
                    "Will diff the changes between the original bundle_nx.ipk and your custom bundle_nx folder, then make a bundle_nx.patchipk\n\n" +

                    "Apply a patchipk: ipktool --input bundle_nx.ipk --patchipk bundle_nx.patchipk\n" +
                    "Will apply the bundle_nx.patchipk patch to bundle_nx.ipk\n\n" +

                    "public FOSS release, enjoy!")
                {
                    inputOption,
                    outputOption,
                    verboseOption,
                    patchipkOption
                };

                rootOption.SetHandler(async (string? input, string? output, bool beverbose, string? patchipk) =>
                {
                    if (input is null || string.IsNullOrWhiteSpace(input))
                        throw new ArgumentException("Invalid input given, please see --help", nameof(input));

                    var extn = Path.GetExtension(input);
                    if (!string.IsNullOrWhiteSpace(patchipk))
                    {
                        var pipkex = Path.GetExtension(patchipk) ??
                            throw new ArgumentException("Must either specify the original ipk, or a patchipk", nameof(patchipk));

                        if (pipkex == ".patchipk")
                            await ApplyPatchipk(input, output, patchipk, beverbose);
                        else if (pipkex == ".ipk")
                            await MakePatchipk(input, output, patchipk, beverbose);
                        else
                            throw new ArgumentException("Invalid file extension", nameof(patchipk));
                    }
                    else if (string.IsNullOrWhiteSpace(extn))
                        await PackIpk(input, output, beverbose);
                    else if (extn == ".ipk")
                        await UnpackIpk(input, output, beverbose);
                    else
                        throw new ArgumentException($"Invalid extension of input, expected .ipk got {extn}", nameof(input));

                    await LoggerConsole("Ipk operation done.");
                    //.
                }, inputOption, outputOption, verboseOption, patchipkOption);

                return await rootOption.InvokeAsync(args);
            }
            catch (Exception exc)
            {
                await LoggerConsole($"An exception had occurred in IpkTool:\n{exc}\n-- Exception end.");
                if (Debugger.IsAttached) throw;
                return 1;
            }
        }

        public static async Task<int> Main(string[] args)
        {
            return await IpkToolMain(args);
        }
    }
}
