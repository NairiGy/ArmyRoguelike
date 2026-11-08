using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    public class KeywordTests
    {
        [Test]
        public void FindsWholeWordsIgnoringCase()
        {
            var found = Keywords.Find("Every enemy next to it is Slowed and pushed 1 tile away.");
            Assert.AreEqual(new[] { Keyword.Slowed, Keyword.Push }, found.Select(f => f.keyword).ToArray());
            Assert.AreEqual("Slowed", "Every enemy next to it is Slowed and pushed 1 tile away.".Substring(found[0].start, found[0].length));
        }

        [Test]
        public void PartsOfOtherWordsAreNotKeywords()
        {
            Assert.IsEmpty(Keywords.Find("unmarked pushover rubbles"));
        }

        [Test]
        public void SeveralWordKeywordsMatchAsOne()
        {
            var found = Keywords.Find("ignores zone of control, needs line of sight");
            Assert.AreEqual(new[] { Keyword.ZoneOfControl, Keyword.LineOfSight }, found.Select(f => f.keyword).ToArray());
            Assert.AreEqual("zone of control".Length, found[0].length);
        }

        [Test]
        public void ShieldInActionNamesIsNotTheKeyword()
        {
            Assert.IsEmpty(Keywords.Find("Shield Up, Shield Bash, Shield Wall and Throw Shield"));
            Assert.AreEqual(new[] { Keyword.Shield, Keyword.Shield }, Keywords.Find("gets a shield; Ward shields the ally").Select(f => f.keyword).ToArray());
        }

        [Test]
        public void FreeCountsOnlyCapitalised()
        {
            Assert.IsEmpty(Keywords.Find("teleport to a free tile"));
            Assert.AreEqual(new[] { Keyword.Free }, Keywords.Find("teleport to a free tile. Free").Select(f => f.keyword).ToArray());
        }

        [Test]
        public void EveryKeywordHasANameAndMeaning()
        {
            foreach (var k in Keywords.All)
            {
                Assert.IsNotEmpty(Keywords.Name(k), k.ToString());
                Assert.IsNotEmpty(Keywords.Describe(k), k.ToString());
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(Keyword)).Length, Keywords.All.Length);
        }

        [Test]
        public void TrimmedTextsStillNameTheirKeywords()
        {
            CollectionAssert.Contains(Keywords.In(Abilities.Describe(ActionKind.FrostNova)), Keyword.Slowed);
            CollectionAssert.Contains(Keywords.In(Abilities.Describe(ActionKind.PinningShot)), Keyword.Pinned);
            CollectionAssert.Contains(Keywords.In(Abilities.Describe(ActionKind.CaveIn)), Keyword.Rubble);
            CollectionAssert.Contains(Keywords.In(Abilities.Describe(ActionKind.Blink)), Keyword.Free);
            CollectionAssert.Contains(Keywords.In(Abilities.Describe(Ability.Elusive)), Keyword.ZoneOfControl);
        }
    }
}
