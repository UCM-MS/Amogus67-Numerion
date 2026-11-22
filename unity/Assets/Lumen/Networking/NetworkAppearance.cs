using System;
using System.Globalization;
using System.Text;
using Unity.Netcode;

namespace Lumen.Networking
{
    [Serializable]
    public struct NetworkAppearance : INetworkSerializable, IEquatable<NetworkAppearance>
    {
        public int SkinTone;
        public int OutfitColor;
        public int HairStyle;
        public int Accessory;

        public NetworkAppearance(int skinTone, int outfitColor, int hairStyle, int accessory = 0)
        {
            SkinTone = skinTone; OutfitColor = outfitColor; HairStyle = hairStyle; Accessory = accessory;
        }

        public NetworkAppearance Sanitized() => new NetworkAppearance(
            Math.Clamp(SkinTone, 0, 7), Math.Clamp(OutfitColor, 0, 7),
            Math.Clamp(HairStyle, 0, 3), Math.Clamp(Accessory, 0, 3));

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SkinTone); serializer.SerializeValue(ref OutfitColor);
            serializer.SerializeValue(ref HairStyle); serializer.SerializeValue(ref Accessory);
        }

        public bool Equals(NetworkAppearance other) => SkinTone == other.SkinTone && OutfitColor == other.OutfitColor
            && HairStyle == other.HairStyle && Accessory == other.Accessory;
        public override bool Equals(object obj) => obj is NetworkAppearance other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SkinTone, OutfitColor, HairStyle, Accessory);
    }

    /// <summary>Shared client/server text policy. Counts Unicode text elements, not UTF-16 halves.</summary>
    public static class PlayerNamePolicy
    {
        public const int MaxTextElements = 20;

        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Objaviteľ";
            // Bound normalization and enumeration work before accepting untrusted remote text.
            if (value.Length > 256) value = value.Substring(0, 256);
            if (value.Length > 0 && char.IsHighSurrogate(value[value.Length - 1])) value = value.Substring(0, value.Length - 1);
            try { value = value.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return "Objaviteľ"; }
            var result = new StringBuilder();
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(value);
            int count = 0, utf8Bytes = 0;
            while (elements.MoveNext() && count < MaxTextElements)
            {
                string element = elements.GetTextElement();
                var category = CharUnicodeInfo.GetUnicodeCategory(element, 0);
                if (category == UnicodeCategory.Control || category == UnicodeCategory.Format
                    || category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator
                    || category == UnicodeCategory.Surrogate || element.IndexOfAny(new[] { '<', '>', '&', '\r', '\n' }) >= 0) continue;
                // FixedString128Bytes leaves 125 bytes of usable UTF-8 storage.
                int bytes = Encoding.UTF8.GetByteCount(element);
                if (utf8Bytes + bytes > 100) break;
                result.Append(element); utf8Bytes += bytes; count++;
            }
            string clean = result.ToString().Trim();
            return string.IsNullOrWhiteSpace(clean) ? "Objaviteľ" : clean;
        }
    }
}
