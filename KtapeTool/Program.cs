using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KtapeTool
{
    public static class Program
    {

        public static async Task<int> Main(string[] args)
        {
            try
            {
                // -- fix output and input on older Windows -- //
                // please always add these lines into your app if you can
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
                System.Console.InputEncoding = System.Text.Encoding.UTF8;
                // -- fix output and input on older Windows -- //

                if (args.Length == 0 || args[0] == "--help")
                {
                    await Console.Out.WriteLineAsync(
                        "KtapeTool - a tool to convert `.ktape.ckd` files into an easier-to-read format and back into weird JSON\n\n" +
                        "Usage: KtapeTool path/to/some.ktape.ckd - unbake a file into some.nik\n" +
                        "       KtapeTool path/to/some.nik - bake a file into some.ktake.ckd");
                    return 0;
                }

                var file = args[0];
                var raw = await File.ReadAllBytesAsync(file);
                var isjson = false;
                if (raw[^1] == 0)
                {
                    raw = raw[..^1];
                    isjson = true;
                }

                var str = Encoding.UTF8.GetString(raw);

                if (isjson)
                {
                    var tape = JsonSerializer.Deserialize<Tape>(str);
                    if (tape is null)
                        throw new Exception("invalid json");

                    // makes the entire thing WAY MORE readable
                    // not sure why Ubisoft aren't doing this automatically????
                    // and yes, the game does still work if you sort the indicies of the karaoke array... >:( o_O?
                    tape.SortClipsByStartTime();

                    var sb = new StringBuilder();
                    // convert into a more readable format for an average person to comprehend
                    tape.ToNikFormat(sb);

                    var newpath = file.Replace(".ktape.ckd", ".nik");
                    await File.WriteAllTextAsync(newpath, sb.ToString());
                    await Console.Out.WriteLineAsync($"Uncooked file written to {newpath}");
                }
                else
                {
                    var tape = new Tape();
                    tape.FromNikFormat(new StringBuilder(str));
                    
                    var options = new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = false
                    };

                    var json = JsonSerializer.Serialize(tape, options);
                    var newpath = file.Replace(".nik", ".ktape.ckd");
                    await File.WriteAllBytesAsync(
                        newpath,
                        // in Ubisoft games all json files have a nullbyte appended at the end
                        // probably to make it easier to read... no idea
                        Encoding.UTF8.GetBytes(json.Append('\0').ToArray())
                    );
                    await Console.Out.WriteLineAsync($"Cooked file written to {newpath}");
                }

                return 0;
            }
            catch (Exception exc)
            {
                await Console.Out.WriteLineAsync($"An exception had occurred:\n{exc}\n-- Exception end.");
                throw;
            }
        }
    }
}