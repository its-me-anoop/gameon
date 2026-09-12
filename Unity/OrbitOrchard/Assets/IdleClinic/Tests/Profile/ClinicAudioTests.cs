using System.Linq;
using IdleClinic.Services;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicAudioTests
    {
        [TestCase("payment")] [TestCase("collect")] [TestCase("care")] [TestCase("consultation")]
        [TestCase("pharmacy")] [TestCase("upgrade")] [TestCase("complete")] [TestCase("tap")]
        [TestCase("footstep")] [TestCase("door")] [TestCase("treatment")] [TestCase("construction")] [TestCase("taxi")]
        public void ActionSoundsAreAvailableShortAudibleAndUnclipped(string name)
        {
            var clip = Resources.Load<AudioClip>("ClinicAudio/" + name);
            Assert.That(clip, Is.Not.Null, "Missing asset would silently remove a shipped effect.");
            Assert.That(clip.length, Is.InRange(.1f, 1.1f));
            var data = Samples(clip);
            Assert.That(data.Max(v => Mathf.Abs(v)), Is.InRange(.02f, .95f));
            Assert.That(data.Sum(v => (double)v * v) / data.Length, Is.GreaterThan(.00001));
            Assert.That(Mathf.Abs(data[0] - data[data.Length-1]), Is.LessThan(.005f));
        }

        [Test] public void OriginalScoreHasACompleteQuietLoopWithoutASeamClick()
        {
            var clip = Resources.Load<AudioClip>("ClinicAudio/morning-rounds");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.EqualTo(48).Within(.001));
            var data = Samples(clip);
            Assert.That(data.Max(v => Mathf.Abs(v)), Is.InRange(.08f, .4f));
            Assert.That(data.Sum(v => (double)v*v) / data.Length, Is.InRange(.0003, .01));
            Assert.That(Mathf.Abs(data[0] - data[data.Length-1]), Is.LessThan(.003f));
        }

        [Test] public void PreferenceAndLifecycleChangesNeverCreateExtraMusicOrEffectSources()
        {
            var go = new GameObject("Clinic audio test");
            try
            {
                var audio = go.AddComponent<ClinicAudio>();
                for (var i = 0; i < 10; i++)
                {
                    audio.Initialize();
                    audio.ApplyPreferences(new ClinicPreferences { music = i % 2 == 0, sound = i % 2 != 0 });
                    Assert.That(audio.MusicEnabled, Is.EqualTo(i % 2 == 0));
                    Assert.That(audio.EffectsEnabled, Is.EqualTo(i % 2 != 0));
                    audio.SetPaused(true); audio.SetPaused(false); audio.ResetWorldObservation();
                }
                var sources = go.GetComponents<AudioSource>();
                Assert.That(sources.Length, Is.EqualTo(ClinicAudio.MaximumVoices + 1));
                Assert.That(sources.Count(s => s.loop), Is.EqualTo(1));
                Assert.That(sources.Single(s => s.loop).clip.name, Is.EqualTo("morning-rounds"));
                Assert.That(sources.All(s => !s.playOnAwake && s.spatialBlend == 0), Is.True);
                audio.ApplyPreferences(new ClinicPreferences { music = false, sound = true });
                Assert.That(sources.Single(s => s.loop).mute, Is.True);
                Assert.That(audio.EffectsEnabled, Is.True);
                audio.ApplyPreferences(new ClinicPreferences { music = true, sound = false });
                Assert.That(sources.Single(s => s.loop).mute, Is.False);
                Assert.That(audio.ActiveEffectVoices, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); }
        }
        private static float[] Samples(AudioClip clip)
        {
            Assert.That(clip.LoadAudioData(), Is.True);
            var data = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(data, 0), Is.True);
            return data;
        }
    }
}
