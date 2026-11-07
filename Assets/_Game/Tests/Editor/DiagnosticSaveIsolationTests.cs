using System;
using System.IO;
using System.Reflection;
using LittleCastle.Research;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class DiagnosticSaveIsolationTests
    {
        GameObject root;
        MeadowSurfacePainter painter;
        static readonly PropertyInfo SavePath=typeof(MeadowSurfacePainter).GetProperty("SavePath",BindingFlags.Instance|BindingFlags.NonPublic);
        [SetUp] public void SetUp()
        {
            root=new GameObject("Diagnostic save isolation test");root.SetActive(false);
            painter=root.AddComponent<MeadowSurfacePainter>();
        }
        [TearDown] public void TearDown(){UnityEngine.Object.DestroyImmediate(root);}

        [Test] public void DefaultSaveRetainsExistingAuthoredMapFilename()
        {
            Assert.That(SavePath.GetValue(painter),Is.EqualTo(Path.Combine(Application.persistentDataPath,"meadow-map-edits-v6.json")));
        }
        [Test] public void DistinctDiagnosticsCannotAliasAuthoredSaveOrEachOther()
        {
            string authored=(string)SavePath.GetValue(painter);
            painter.diagnosticSaveId="river-water-study";string river=(string)SavePath.GetValue(painter);
            painter.diagnosticSaveId="ground-study";string ground=(string)SavePath.GetValue(painter);
            Assert.That(river,Is.Not.EqualTo(authored));Assert.That(ground,Is.Not.EqualTo(authored));
            Assert.That(ground,Is.Not.EqualTo(river));Assert.That(Path.GetDirectoryName(river),Is.EqualTo(Path.GetFullPath(Application.persistentDataPath)));
        }
        [TestCase("../meadow-map-edits")]
        [TestCase("..\\meadow-map-edits")]
        [TestCase("E:/map")]
        public void DiagnosticIdsCannotEscapeTheirSaveDirectory(string id)
        {
            painter.diagnosticSaveId=id;
            var error=Assert.Throws<TargetInvocationException>(()=>SavePath.GetValue(painter));
            Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
        }
    }
}
