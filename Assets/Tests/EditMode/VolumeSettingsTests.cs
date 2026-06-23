using NUnit.Framework;
using Proto.Core;

namespace Proto.Tests
{
    public class VolumeSettingsTests
    {
        [Test]
        public void Defaults_AreInRange()
        {
            var v = new VolumeSettings();
            Assert.GreaterOrEqual(v.Music, 0f); Assert.LessOrEqual(v.Music, 1f);
            Assert.GreaterOrEqual(v.Sfx, 0f);   Assert.LessOrEqual(v.Sfx, 1f);
        }

        [Test]
        public void Setters_ClampToUnitRange()
        {
            var v = new VolumeSettings();
            v.Music = 5f;  Assert.AreEqual(1f, v.Music);
            v.Sfx = -3f;   Assert.AreEqual(0f, v.Sfx);
        }

        [Test]
        public void Changed_FiresOnRealChange_NotOnSameValue()
        {
            var v = new VolumeSettings(music: 0.5f, sfx: 0.5f);
            int fired = 0;
            v.Changed += () => fired++;

            v.Music = 0.7f;   // change
            v.Music = 0.7f;   // same value -> no event
            v.Sfx = 0.2f;     // change

            Assert.AreEqual(2, fired);
        }
    }
}
