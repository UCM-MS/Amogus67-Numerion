using System;
using System.Globalization;
using Lumen.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Lumen.Tests
{
    public sealed class NetworkValidationTests
    {
        [Test]
        public void SequenceOrderingRejectsReplayAndHandlesUintWrap()
        {
            Assert.That(NetworkMovement.IsNewer(5, 5), Is.False);
            Assert.That(NetworkMovement.IsNewer(4, 5), Is.False);
            Assert.That(NetworkMovement.IsNewer(6, 5), Is.True);
            Assert.That(NetworkMovement.IsNewer(0, uint.MaxValue), Is.True);
            Assert.That(NetworkMovement.IsNewer(uint.MaxValue, 0), Is.False);
        }

        [Test]
        public void InputCannotInjectNanInfiniteOrOversizedMotion()
        {
            Assert.That(NetworkMovement.TrySanitize(new ExplorerInput { Move = new Vector2(float.NaN, 0) }, out _), Is.False);
            Assert.That(NetworkMovement.TrySanitize(new ExplorerInput { Yaw = float.PositiveInfinity }, out _), Is.False);
            Assert.That(NetworkMovement.TrySanitize(new ExplorerInput { Move = new Vector2(20, 0) }, out _), Is.False);
            Assert.That(NetworkMovement.TrySanitize(new ExplorerInput { Move = Vector2.one, Yaw = -10 }, out var safe), Is.True);
            Assert.That(safe.Move.magnitude, Is.EqualTo(1).Within(.0001));
            Assert.That(safe.Yaw, Is.EqualTo(350).Within(.0001));
        }

        [Test]
        public void DisplayNamesKeepUnicodeWholeAndRemoveControlMarkup()
        {
            string emoji = string.Concat(System.Linq.Enumerable.Repeat("🚀", 30));
            string clean = PlayerNamePolicy.Sanitize(emoji);
            Assert.That(new StringInfo(clean).LengthInTextElements, Is.EqualTo(20));
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(clean), Is.LessThanOrEqualTo(100));
            clean = PlayerNamePolicy.Sanitize("<b>Žofia\n\u202e</b>");
            Assert.That(clean.IndexOf("<", StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(clean.IndexOf(">", StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(clean.IndexOf("\n", StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(clean.IndexOf("\u202e", StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(PlayerNamePolicy.Sanitize("\uD800"), Is.EqualTo("Objaviteľ"));
        }

        [Test]
        public void AppearanceIdsCannotEscapeRegisteredPalette()
        {
            var safe = new NetworkAppearance(-5, int.MaxValue, 99, -99).Sanitized();
            Assert.That(safe.SkinTone, Is.EqualTo(0));
            Assert.That(safe.OutfitColor, Is.EqualTo(7));
            Assert.That(safe.HairStyle, Is.EqualTo(3));
            Assert.That(safe.Accessory, Is.EqualTo(0));
        }

    }
}
