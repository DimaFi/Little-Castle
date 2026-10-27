using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapBrushPresetTests
    {
        [Test] public void MapTools_PresetAssetReopensWithItsOwnMonoScriptAndSettings()
        {
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/BrushPreset_Verification.asset");
            var preset=ScriptableObject.CreateInstance<MapBrushPreset>();
            try
            {
                preset.toolId="terrain.raise";preset.settings.radius=24;preset.settings.strength=.35f;preset.settings.softness=.7f;
                AssetDatabase.CreateAsset(preset,path);AssetDatabase.SaveAssetIfDirty(preset);Resources.UnloadAsset(preset);preset=null;
                var reopened=AssetDatabase.LoadAssetAtPath<MapBrushPreset>(path);
                Assert.That(reopened,Is.Not.Null);Assert.That(reopened.toolId,Is.EqualTo("terrain.raise"));
                Assert.That(reopened.settings.radius,Is.EqualTo(24));Assert.That(reopened.settings.strength,Is.EqualTo(.35f));Assert.That(reopened.settings.softness,Is.EqualTo(.7f));
            }
            finally{AssetDatabase.DeleteAsset(path);if(preset)Object.DestroyImmediate(preset);}
        }
    }
}
