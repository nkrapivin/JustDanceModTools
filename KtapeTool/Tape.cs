using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KtapeTool
{
    public class Tape : JDObject
    {
        public KaraokeClip[] Clips { get; set; } = Array.Empty<KaraokeClip>();
        public int TapeClock { get; set; }
        public int TapeBarCount { get; set; }
        public int FreeResourcesAfterPlay { get; set; }
        public string MapName { get; set; } = "";
        public string SoundwichEvent { get; set; } = "";

        public void SortClipsByStartTime()
        {
            Array.Sort(Clips);
        }

        public void FromNikFormat(StringBuilder sb)
        {
            using var sr = new StringReader(sb.ToString());
            while (sr.ReadLine() is string line)
            {
                if (line.StartsWith(';'))
                    continue;

                var eqind = line.IndexOf('=');
                if (eqind < 0)
                    continue;

                var name = line[..eqind];
                var value = line[(eqind + 1)..];

                switch (name)
                {
                    case "__class":
                        {
                            __class = value.Replace('\u00A0', ' ');
                            break;
                        }

                    case "Clips":
                        {
                            Clips = new KaraokeClip[uint.Parse(value, CultureInfo.InvariantCulture)];
                            break;
                        }

                    case "TapeClock":
                        {
                            TapeClock = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        }

                    case "TapeBarCount":
                        {
                            TapeBarCount = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        }

                    case "FreeResourcesAfterPlay":
                        {
                            FreeResourcesAfterPlay = int.Parse(value, CultureInfo.InvariantCulture);
                            break;
                        }

                    case "MapName":
                        {
                            MapName = value.Replace('\u00A0', ' ');
                            break;
                        }

                    case "SoundwichEvent":
                        {
                            SoundwichEvent = value.Replace('\u00A0', ' ');
                            break;
                        }

                    default:
                        {
                            var arrind1 = name.IndexOf('[');
                            var arrind2 = name.IndexOf(']');
                            if (arrind1 < 0 || arrind2 < 0)
                            {
                                // most likely just "EOF", ignore.
                                break;
                            }

                            var arrname = name[..arrind1];
                            var arrind = uint.Parse(name.Substring(arrind1 + 1, arrind2 - arrind1 - 1), CultureInfo.InvariantCulture);

                            switch (arrname)
                            {
                                case "Clips":
                                    {
                                        var arrvalue = new KaraokeClip();
                                        arrvalue.FromNikFormat(new StringBuilder(value));
                                        Clips[arrind] = arrvalue;
                                        break;
                                    }

                                default:
                                    {
                                        // ???
                                        break;
                                    }
                            }

                            break;
                        }
                }
            }
        }

        public void ToNikFormat(StringBuilder sb)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"__class={__class}");

            sb.AppendLine(CultureInfo.InvariantCulture, $"Clips={Clips.Length}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"; Clips[index]=__class,Id,TrackId,IsActive,StartTime,Duration,Pitch,Lyrics,IsEndOfLine,ContentType,StartTimeTolerance,EndTimeTolerance,SemitoneTolerance");

            for (int i = 0; i < Clips.Length; ++i)
            {
                sb.Append(CultureInfo.InvariantCulture, $"Clips[{i}]=");
                Clips[i].ToNikFormat(sb);
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"TapeClock={TapeClock}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"TapeBarCount={TapeBarCount}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"FreeResourcesAfterPlay={FreeResourcesAfterPlay}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"MapName={MapName}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"SoundwichEvent={SoundwichEvent}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"EOF");
        }
    }
}
