using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KtapeTool
{
    public class KaraokeClip : JDObject, IComparable, IComparable<KaraokeClip>
    {
        public uint Id { get; set; }
        public int TrackId { get; set; }
        public int IsActive { get; set; }
        public int StartTime { get; set; }
        public int Duration { get; set; }
        public double Pitch { get; set; }
        public string Lyrics { get; set; } = "";
        public int IsEndOfLine { get; set; }
        public int ContentType { get; set; }
        public int StartTimeTolerance { get; set; }
        public int EndTimeTolerance { get; set; }
        public int SemitoneTolerance { get; set; }

        public void FromNikFormat(StringBuilder sb)
        {
            var items = sb.ToString().Split(',', StringSplitOptions.None);
            __class = items[0].Replace('\u00A0', ' ');
            Id = uint.Parse(items[1], CultureInfo.InvariantCulture);
            TrackId = int.Parse(items[2], CultureInfo.InvariantCulture);
            IsActive = int.Parse(items[3], CultureInfo.InvariantCulture);
            StartTime = int.Parse(items[4], CultureInfo.InvariantCulture);
            Duration = int.Parse(items[5], CultureInfo.InvariantCulture);
            Pitch = double.Parse(items[6], CultureInfo.InvariantCulture);
            Lyrics = items[7].Replace('\u00A0', ' ');
            IsEndOfLine = int.Parse(items[8], CultureInfo.InvariantCulture);
            ContentType = int.Parse(items[9], CultureInfo.InvariantCulture);
            StartTimeTolerance = int.Parse(items[10], CultureInfo.InvariantCulture);
            EndTimeTolerance = int.Parse(items[11], CultureInfo.InvariantCulture);
            SemitoneTolerance = int.Parse(items[12], CultureInfo.InvariantCulture);
        }

        public void ToNikFormat(StringBuilder sb)
        {
            // oh no
            if (Lyrics.Contains(',') || Lyrics.Contains('=') || Lyrics.Contains('\n'))
                throw new InvalidDataException($"Lyric line '{Lyrics}' contains invalid characters");

            // still way easier to read than json, especially if you have multi-line select in your text editor
            sb.AppendLine(CultureInfo.InvariantCulture, $"{__class},{Id},{TrackId},{IsActive},{StartTime},{Duration},{Pitch},{Lyrics},{IsEndOfLine},{ContentType},{StartTimeTolerance},{EndTimeTolerance},{SemitoneTolerance}");
        }

        int IComparable.CompareTo(object? obj)
        {
            // this is how those checks seem to be implemented in the standard library e.g. System.Int32...
            if (obj is not KaraokeClip)
                throw new ArgumentException("Object must be of type KaraokeClip.", nameof(obj));
            // you usually do not throw in this case, but for this tool we don't care and rather crash
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));
            // just call the comparers for StartTime
            return StartTime.CompareTo(((KaraokeClip)obj).StartTime);
        }

        int IComparable<KaraokeClip>.CompareTo(KaraokeClip? other)
        {
            // too lazy, just call the more generic version
            return ((IComparable)this).CompareTo(other);
        }
    }
}
