using System.Collections;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class MapTreePlayModeTests
    {
        [UnityTest] public IEnumerator MapTrees_PresenterBudgetIdentitySelectionHeightAndCleanup()
        {
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);source.name="Synthetic Oak";source.AddComponent<LODGroup>();
            var catalog=ScriptableObject.CreateInstance<MapTreeCatalog>();var definition=new MapTreeDefinition{archetypeId="oak.test"};
            catalog.entries=new[]{new MapTreeCatalog.Entry{title="Test Oak",prefab=source,definition=definition,localBounds=new Bounds(Vector3.zero,Vector3.one)}};
            catalog.packs=new[]{new MapTreePack{id="test",entries=new List<MapTreePackEntry>{new MapTreePackEntry{archetypeId="oak.test"}}}};
            MapTreePresentation view=null;
            try
            {
                var session=new MapEditSession(new MapAuthoringDocument{mapId="tree-play",bounds=new Vector4(-64,-64,128,128),heightField=new MapHeightFieldData()});
                session.RegisterTreeCatalog(catalog.Definitions,catalog.packs);long near=session.PlaceTree("oak.test",new Vector2(8,8));session.PlaceTree("oak.test",new Vector2(40,40));
                view=new MapTreePresentation(session,catalog){maximumResident=1,creationBudget=1,maximumDistance=25};Assert.That(source.activeSelf,Is.False);
                view.Tick(Vector3.zero,true);Assert.That(view.ResidentCount,Is.EqualTo(1));var instance=view.Root(near);Assert.That(instance,Is.Not.Null);
                Assert.That(instance.activeSelf,Is.True);
                Assert.That(instance.GetComponent<MapTreeInstanceView>().stableId,Is.EqualTo(near));
                Assert.That(view.Pick(new Ray(new Vector3(8,10,8),Vector3.down),out long picked),Is.True);Assert.That(picked,Is.EqualTo(near));
                session.BeginHeightStroke(4,1);session.AppendHeightPoint(new Vector2(8,8));session.CommitStroke();Assert.That(view.Position(near).y,Is.EqualTo(1));Assert.That(view.Root(near),Is.SameAs(instance));
                session.TransformTree(near,new Vector2(12,8),45,1.1f);Assert.That(view.Position(near).x,Is.EqualTo(12));Assert.That(view.Root(near),Is.SameAs(instance));
                session.DeleteTree(near);view.Tick(Vector3.zero,true);Assert.That(view.ResidentCount,Is.Zero);session.Undo();view.Tick(Vector3.zero,true);Assert.That(view.ResidentCount,Is.EqualTo(1));
                view.Dispose();view=null;Assert.That(source.activeSelf,Is.True);Assert.That(source.transform.position,Is.EqualTo(Vector3.zero));Assert.That(source.transform.localScale,Is.EqualTo(Vector3.one));
                yield return null;
            }
            finally{view?.Dispose();Object.Destroy(source);Object.Destroy(catalog);}
            yield return null;
        }
        [UnityTest] public IEnumerator MapTrees_CrownPickSkipsEmptyCornerOfNeighbourBounds()
        {
            var catalog=MapTreeCatalog.Default;
            var session=new MapEditSession(new MapAuthoringDocument{mapId="native-pick",bounds=new Vector4(-64,-64,128,128),heightField=new MapHeightFieldData()});
            session.RegisterTreeCatalog(catalog.Definitions,catalog.packs);
            long target=session.PlaceTree("oak.a",new Vector2(-15,-6),135,1.1f);
            session.PlaceTree("oak.b",new Vector2(-6,-12),47,1);
            using(var view=new MapTreePresentation(session,catalog))
            {
                var eye=new Vector3(8,7.8f,-10);view.Tick(eye,true);
                var aim=view.Position(target)+Vector3.up*3;
                Assert.That(view.Pick(new Ray(eye,(aim-eye).normalized),out long picked),Is.True);
                Assert.That(picked,Is.EqualTo(target));
            }
            yield return null;
        }
    }
}
