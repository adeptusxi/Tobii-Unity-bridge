using UnityEngine;

namespace TalonBridge.Gaze.Udp
{
    /// <summary>
    /// Decodes the bridge's JSON datagrams without allocating.
    ///
    /// The wire format is a flat JSON object with a known, small set of keys, so
    /// this reads the bytes directly rather than going through a general JSON
    /// parser: gaze arrives at up to ~90 Hz and must not produce garbage every
    /// packet. Unknown keys are skipped, so the parser tolerates a bridge that
    /// sends more fields than this version knows about.
    ///
    /// One instance per receiver; not thread-safe.
    /// </summary>
    public sealed class GazePacketParser
    {
        private const int MaxTitleLength = 64;

        private readonly char[] titleBuffer = new char[MaxTitleLength];
        private string cachedTitle = string.Empty;

        /// <summary>Description of the last failure, for diagnostics.</summary>
        public string LastError { get; private set; } = string.Empty;

        public bool TryParse(byte[] data, int length, out GazePacket packet)
        {
            packet = default;
            packet.WindowTitle = string.Empty;

            int i = 0;
            SkipWhitespace(data, length, ref i);
            if (i >= length || data[i] != (byte)'{')
            {
                return Fail("packet is not a JSON object", out packet);
            }

            i++;
            while (true)
            {
                SkipWhitespace(data, length, ref i);
                if (i >= length)
                {
                    return Fail("truncated packet", out packet);
                }

                if (data[i] == (byte)'}')
                {
                    return true;
                }

                if (data[i] != (byte)'"')
                {
                    return Fail("expected a key", out packet);
                }

                int keyStart = ++i;
                while (i < length && data[i] != (byte)'"')
                {
                    i++;
                }

                if (i >= length)
                {
                    return Fail("unterminated key", out packet);
                }

                int keyLength = i - keyStart;
                i++;

                SkipWhitespace(data, length, ref i);
                if (i >= length || data[i] != (byte)':')
                {
                    return Fail("expected ':'", out packet);
                }

                i++;
                SkipWhitespace(data, length, ref i);

                if (!ReadValue(data, length, ref i, keyStart, keyLength, ref packet))
                {
                    packet = default;
                    return false;
                }

                SkipWhitespace(data, length, ref i);
                if (i < length && data[i] == (byte)',')
                {
                    i++;
                    continue;
                }

                if (i < length && data[i] == (byte)'}')
                {
                    return true;
                }

                return Fail("expected ',' or '}'", out packet);
            }
        }

        private bool ReadValue(
            byte[] data, int length, ref int i, int keyStart, int keyLength, ref GazePacket packet)
        {
            if (KeyIs(data, keyStart, keyLength, "sx"))
            {
                if (!ReadNumber(data, length, ref i, out double x)) return Fail("bad sx");
                packet.ScreenNormalized.x = (float)x;
                packet.HasScreenNormalized = true;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "sy"))
            {
                if (!ReadNumber(data, length, ref i, out double y)) return Fail("bad sy");
                packet.ScreenNormalized.y = (float)y;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "wx"))
            {
                if (!ReadNumber(data, length, ref i, out double x)) return Fail("bad wx");
                packet.WindowNormalized.x = (float)x;
                packet.HasWindowNormalized = true;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "wy"))
            {
                if (!ReadNumber(data, length, ref i, out double y)) return Fail("bad wy");
                packet.WindowNormalized.y = (float)y;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "ok"))
            {
                if (!ReadNumber(data, length, ref i, out double ok)) return Fail("bad ok");
                packet.Ok = ok != 0d;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "in"))
            {
                if (!ReadNumber(data, length, ref i, out double inside)) return Fail("bad in");
                packet.InsideWindow = inside != 0d;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "eyes"))
            {
                if (!ReadNumber(data, length, ref i, out double eyes)) return Fail("bad eyes");
                packet.Eyes = (GazeEyes)(int)eyes;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "t"))
            {
                if (!ReadNumber(data, length, ref i, out double t)) return Fail("bad t");
                packet.SourceTimestamp = t;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "wall"))
            {
                if (!ReadNumber(data, length, ref i, out double wall)) return Fail("bad wall");
                packet.WallTime = wall;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "v"))
            {
                if (!ReadNumber(data, length, ref i, out double v)) return Fail("bad v");
                packet.Version = (int)v;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "scr"))
            {
                if (!ReadRect(data, length, ref i, out Rect rect)) return Fail("bad scr");
                packet.ScreenRect = rect;
                packet.HasScreenRect = true;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "win"))
            {
                if (!ReadRect(data, length, ref i, out Rect rect)) return Fail("bad win");
                packet.WindowRect = rect;
                packet.HasWindowRect = true;
                return true;
            }

            if (KeyIs(data, keyStart, keyLength, "title"))
            {
                if (!ReadTitle(data, length, ref i, out string title)) return Fail("bad title");
                packet.WindowTitle = title;
                return true;
            }

            return SkipValue(data, length, ref i) || Fail("unparseable value");
        }

        private static bool KeyIs(byte[] data, int start, int length, string key)
        {
            if (length != key.Length)
            {
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                if (data[start + i] != (byte)key[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void SkipWhitespace(byte[] data, int length, ref int i)
        {
            while (i < length)
            {
                byte b = data[i];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r')
                {
                    return;
                }

                i++;
            }
        }

        private static bool ReadNumber(byte[] data, int length, ref int i, out double value)
        {
            value = 0d;
            int start = i;
            bool negative = false;

            if (i < length && (data[i] == (byte)'-' || data[i] == (byte)'+'))
            {
                negative = data[i] == (byte)'-';
                i++;
            }

            double integer = 0d;
            int digits = 0;
            while (i < length && data[i] >= (byte)'0' && data[i] <= (byte)'9')
            {
                integer = integer * 10d + (data[i] - (byte)'0');
                digits++;
                i++;
            }

            double fraction = 0d;
            if (i < length && data[i] == (byte)'.')
            {
                i++;
                double scale = 0.1d;
                while (i < length && data[i] >= (byte)'0' && data[i] <= (byte)'9')
                {
                    fraction += (data[i] - (byte)'0') * scale;
                    scale *= 0.1d;
                    digits++;
                    i++;
                }
            }

            if (digits == 0)
            {
                i = start;
                return false;
            }

            value = integer + fraction;

            if (i < length && (data[i] == (byte)'e' || data[i] == (byte)'E'))
            {
                i++;
                bool negativeExponent = false;
                if (i < length && (data[i] == (byte)'-' || data[i] == (byte)'+'))
                {
                    negativeExponent = data[i] == (byte)'-';
                    i++;
                }

                int exponent = 0;
                int exponentDigits = 0;
                while (i < length && data[i] >= (byte)'0' && data[i] <= (byte)'9')
                {
                    exponent = exponent * 10 + (data[i] - (byte)'0');
                    exponentDigits++;
                    i++;
                }

                if (exponentDigits == 0)
                {
                    i = start;
                    return false;
                }

                value *= System.Math.Pow(10d, negativeExponent ? -exponent : exponent);
            }

            if (negative)
            {
                value = -value;
            }

            return true;
        }

        private static bool ReadRect(byte[] data, int length, ref int i, out Rect rect)
        {
            rect = default;
            if (i >= length || data[i] != (byte)'[')
            {
                return false;
            }

            i++;
            if (!ReadRectComponent(data, length, ref i, out double x, expectComma: true) ||
                !ReadRectComponent(data, length, ref i, out double y, expectComma: true) ||
                !ReadRectComponent(data, length, ref i, out double w, expectComma: true) ||
                !ReadRectComponent(data, length, ref i, out double h, expectComma: false))
            {
                return false;
            }

            if (i >= length || data[i] != (byte)']')
            {
                return false;
            }

            i++;
            rect = new Rect((float)x, (float)y, (float)w, (float)h);
            return true;
        }

        private static bool ReadRectComponent(
            byte[] data, int length, ref int i, out double value, bool expectComma)
        {
            SkipWhitespace(data, length, ref i);
            if (!ReadNumber(data, length, ref i, out value))
            {
                return false;
            }

            SkipWhitespace(data, length, ref i);
            if (!expectComma)
            {
                return true;
            }

            if (i >= length || data[i] != (byte)',')
            {
                return false;
            }

            i++;
            return true;
        }

        /// <summary>
        /// Reads the window title, reusing the previous string when the title is
        /// unchanged so that the steady state allocates nothing.
        /// </summary>
        private bool ReadTitle(byte[] data, int length, ref int i, out string title)
        {
            title = string.Empty;
            if (i >= length || data[i] != (byte)'"')
            {
                return false;
            }

            i++;
            int count = 0;
            while (i < length && data[i] != (byte)'"')
            {
                if (data[i] == (byte)'\\' && i + 1 < length)
                {
                    i++;
                }

                if (count < MaxTitleLength)
                {
                    titleBuffer[count++] = (char)data[i];
                }

                i++;
            }

            if (i >= length)
            {
                return false;
            }

            i++;

            if (!SameAsCachedTitle(count))
            {
                cachedTitle = new string(titleBuffer, 0, count);
            }

            title = cachedTitle;
            return true;
        }

        private bool SameAsCachedTitle(int count)
        {
            if (cachedTitle.Length != count)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (cachedTitle[i] != titleBuffer[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SkipValue(byte[] data, int length, ref int i)
        {
            if (i >= length)
            {
                return false;
            }

            byte b = data[i];
            if (b == (byte)'"')
            {
                i++;
                while (i < length && data[i] != (byte)'"')
                {
                    if (data[i] == (byte)'\\' && i + 1 < length)
                    {
                        i++;
                    }

                    i++;
                }

                if (i >= length)
                {
                    return false;
                }

                i++;
                return true;
            }

            if (b == (byte)'[' || b == (byte)'{')
            {
                byte open = b;
                byte close = b == (byte)'[' ? (byte)']' : (byte)'}';
                int depth = 0;
                while (i < length)
                {
                    if (data[i] == open)
                    {
                        depth++;
                    }
                    else if (data[i] == close)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            i++;
                            return true;
                        }
                    }

                    i++;
                }

                return false;
            }

            // A bare literal: number, true, false or null.
            int start = i;
            while (i < length && data[i] != (byte)',' && data[i] != (byte)'}')
            {
                i++;
            }

            return i > start;
        }

        private bool Fail(string reason)
        {
            LastError = reason;
            return false;
        }

        private bool Fail(string reason, out GazePacket packet)
        {
            packet = default;
            return Fail(reason);
        }
    }
}
