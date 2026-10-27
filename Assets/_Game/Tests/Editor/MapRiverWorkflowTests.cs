using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapRiverWorkflowTests
    {
        static MapEditSession Session(float baseHeight=0)=>new MapEditSession(new MapAuthoringDocument
        {
            mapId="river-workflow",bounds=new Vector4(-128,-128,256,256),
            heightField=new MapHeightFieldData{baseHeight=baseHeight}
        });
        static MapRiverToolState NewDraft(MapEditSession session)
        {
            var state=new MapRiverToolState();state.StartRiver();
            state.Click(session,new Vector2(0,-20));state.Click(session,new Vector2(0,20));
            return state;
        }
        [Test] public void ControlPointController_CommitsOnlyOnApply_AndSharedHistoryKeepsIds()
        {
            var session=Session();var state=new MapRiverToolState();var registry=MapToolRegistry.ExistingBrushes();
            registry.Register(new MapClickTool("river.points","River","Water",p=>state.Click(session,p.position),true,MapToolGesture.ControlPoints));
            var controller=new MapToolController(session,registry);controller.Select("river.points");state.StartRiver();
            controller.Begin(new MapToolPointer(default,true,new Vector2(0,-20)),0);
            controller.Move(new MapToolPointer(default,true,new Vector2(0,20)));controller.Tick(1);
            Assert.That(controller.Active,Is.False);Assert.That(state.draft.nodes.Count,Is.EqualTo(1));
            Assert.That(state.ApplyRiver(session),Is.False);Assert.That(session.CanUndo,Is.False);
            controller.Begin(new MapToolPointer(default,true,new Vector2(0,20)),2);
            long riverId=state.draft.id,firstNodeId=state.draft.nodes[0].id;
            Assert.That(session.RiverCount,Is.Zero);Assert.That(state.ApplyRiver(session),Is.True,state.status);
            Assert.That(session.RiverCount,Is.EqualTo(1));
            controller.Undo();state.Refresh(session);Assert.That(session.RiverCount,Is.Zero);Assert.That(state.draft,Is.Null);
            controller.Redo();state.Refresh(session);Assert.That(state.draft.id,Is.EqualTo(riverId));
            Assert.That(state.draft.nodes[0].id,Is.EqualTo(firstNodeId));
        }
        [Test] public void SelectionMoveApplyDelete_SaveLoadAndSharedHistoryStayConsistent()
        {
            var session=Session();var state=NewDraft(session);Assert.That(state.ApplyRiver(session),Is.True,state.status);
            long id=state.draft.id;state.StartSelection();state.Click(session,Vector2.zero);
            Assert.That(state.draft.id,Is.EqualTo(id));state.node=1;state.mode=6;
            state.Click(session,new Vector2(0,30));Assert.That(session.GetRiver(id).nodes[1].position.y,Is.EqualTo(20));
            Assert.That(state.ApplyRiver(session),Is.True,state.status);
            string saved=session.ExportJson();session.DeleteRiver(id);state.Refresh(session);Assert.That(state.draft,Is.Null);
            session.Undo();state.Refresh(session);Assert.That(state.draft.nodes[1].position.y,Is.EqualTo(30));
            session.Undo();state.Refresh(session);Assert.That(state.draft.nodes[1].position.y,Is.EqualTo(20));
            session.Redo();state.Refresh(session);Assert.That(session.ExportJson(),Is.EqualTo(saved));
            var fresh=Session();fresh.LoadJson(saved);Assert.That(fresh.ExportJson(),Is.EqualTo(saved));
            state.draft.nodes[0].width=12;Assert.That(state.HasUnappliedDraft(session),Is.True);
            session.LoadJson(saved);state.Refresh(session);Assert.That(state.draft.id,Is.EqualTo(id));
            Assert.That(state.draft.nodes[0].width,Is.EqualTo(9));Assert.That(state.HasUnappliedDraft(session),Is.False);
        }
        [Test] public void NewRiverAtMinimumTerrainHeight_UsesValidDefaultBed()
        {
            var session=Session(-4);var state=NewDraft(session);
            Assert.That(state.draft.nodes[0].waterHeight-state.draft.nodes[0].depth,Is.GreaterThanOrEqualTo(session.MinimumHeight));
            Assert.That(state.ApplyRiver(session),Is.True,state.status);
        }
        [Test] public void DoubleClickDoesNotCreateAnInvalidAdjacentNode_AndStoppedToolIgnoresClicks()
        {
            var session=Session();var state=NewDraft(session);state.Click(session,new Vector2(0,20.1f));
            Assert.That(state.draft.nodes.Count,Is.EqualTo(2));Assert.That(state.status,Does.Contain("2–1024"));
            Assert.That(state.ApplyRiver(session),Is.True,state.status);state.Stop();state.draft=null;
            state.Click(session,Vector2.zero);Assert.That(state.draft,Is.Null);
        }
        [Test] public void DraftSurvivesReloadAndUnrelatedHistory_WithoutBecomingAuthoritative()
        {
            var session=Session();var state=NewDraft(session);long id=state.draft.id;state.Stop();
            var restored=new MapRiverToolState();Assert.That(restored.RestoreDraftJson(state.ExportDraftJson()),Is.True);
            restored.Refresh(session);Assert.That(restored.IsCreatingRiver,Is.True);Assert.That(restored.draft.id,Is.EqualTo(id));
            session.BeginHeightStroke(2,.2f);session.AppendHeightPoint(new Vector2(60,60));session.CommitStroke();
            session.Undo();restored.Refresh(session);Assert.That(restored.draft.nodes.Count,Is.EqualTo(2));
            Assert.That(session.RiverCount,Is.Zero);Assert.That(restored.ApplyRiver(session),Is.True,restored.status);
            Assert.That(session.GetRiver(id).nodes.Count,Is.EqualTo(2));
        }
        [Test] public void ReplacedMapSessionDropsOldDraft_AndPlayerApplyStillChecksPermissions()
        {
            var session=Session();var state=NewDraft(session);session.Access=MapEditAccess.Player;
            Assert.That(state.ApplyRiver(session),Is.False);Assert.That(session.RiverCount,Is.Zero);
            state.Refresh(Session());Assert.That(state.draft,Is.Null);Assert.That(state.mode,Is.Zero);
        }
        [Test] public void EditedCommittedRiverDraftSurvivesWindowReload_UntilExplicitApply()
        {
            var session=Session();var state=NewDraft(session);Assert.That(state.ApplyRiver(session),Is.True,state.status);
            state.draft.nodes[0].width=12;state.Stop();var restored=new MapRiverToolState();
            Assert.That(restored.RestoreDraftJson(state.ExportDraftJson()),Is.True);restored.Refresh(session);
            Assert.That(restored.draft.nodes[0].width,Is.EqualTo(12));Assert.That(restored.HasUnappliedDraft(session),Is.True);
            Assert.That(session.GetRiver(restored.draft.id).nodes[0].width,Is.EqualTo(9));
            Assert.That(restored.ApplyRiver(session),Is.True,restored.status);
            Assert.That(session.GetRiver(restored.draft.id).nodes[0].width,Is.EqualTo(12));
        }
        [Test] public void WaterOnlyProfileIsExplicitForNewRivers_AndDiscardKeepsCommittedMap()
        {
            var session=Session();var state=NewDraft(session);Assert.That(state.ApplyRiver(session),Is.True,state.status);
            long existing=state.draft.id;state.ConfigureNewRiverProfile(true);
            Assert.That(session.GetRiver(existing).layoutVersion,Is.EqualTo(1));
            state.StartRiver();state.Click(session,new Vector2(40,-20));state.Click(session,new Vector2(40,20));
            Assert.That(state.draft.layoutVersion,Is.EqualTo(2));Assert.That(state.draft.nodes[0].leftBank,Is.EqualTo(MapBankKind.Sand));
            Assert.That(state.HasUnappliedDraft(session),Is.True);state.DiscardDraft();
            Assert.That(state.HasUnappliedDraft(session),Is.False);Assert.That(state.mode,Is.Zero);
            Assert.That(session.RiverCount,Is.EqualTo(1));Assert.That(session.GetRiver(existing).layoutVersion,Is.EqualTo(1));
        }
    }
}
