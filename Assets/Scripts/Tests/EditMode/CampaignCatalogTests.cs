using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class CampaignCatalogTests
    {
        [Test] public void DefaultCatalogAppendsThreeCargoLevelsAfterOriginalCampaign()
        {
            var catalog = Resources.Load<CampaignCatalog>("configs/CampaignCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ReadLevels().Select(level => level.id), Is.EqualTo(new[] { "L01", "L02", "L03", "L04", "L05", "L06" }));
            var copy = catalog.ReadLevels(); copy[0].title = "changed";
            Assert.That(catalog.ReadLevels()[0].title, Is.EqualTo("唤醒维修区"));
        }
        [Test] public void CatalogOrderDeterminesNavigationOrder()
        {
            var catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
            try
            {
                SetEntries(catalog, Resources.Load<TextAsset>("configs/levels/L03"), Resources.Load<TextAsset>("configs/levels/L01"));
                Assert.That(catalog.ReadLevels().Select(level => level.id), Is.EqualTo(new[] { "L03", "L01" }));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
        [Test] public void MissingAndDuplicateEntriesFailBeforeLoadingAnyBoard()
        {
            var catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => catalog.ReadLevels());
                SetEntries(catalog, new TextAsset[] { null });
                Assert.Throws<InvalidOperationException>(() => catalog.ReadLevels());
                var first = Resources.Load<TextAsset>("configs/levels/L01"); SetEntries(catalog, first, first);
                Assert.Throws<InvalidOperationException>(() => catalog.ReadLevels());
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
        private static void SetEntries(CampaignCatalog catalog, params TextAsset[] entries)
        {
            var serialized = new SerializedObject(catalog); var levels = serialized.FindProperty("levels"); levels.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++) levels.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
