using System;
namespace LittleCastle.World
{
    /// <summary>Detached preview until mouse release. Escape never needs a compensating Undo.</summary>
    public sealed class MapRiverEditGesture
    {
        readonly MapEditSession session;
        readonly long riverRevision,heightRevision;
        readonly MapRiverRecord original;
        public MapRiverRecord Draft {get;private set;}
        public MapRiverEditSolution LastValid {get;private set;}
        public MapEditSession PreviewSession {get;private set;}
        public bool Valid {get;private set;}
        public string Message {get;private set;}
        public double LastSolveMilliseconds {get;private set;}
        public double LastPreviewApplyMilliseconds {get;private set;}
        public MapRiverEditGesture(MapEditSession session,MapRiverRecord draft)
        {this.session=session;riverRevision=session.RiverRevision;heightRevision=session.HeightRevision;original=MapRiverNodeEditing.Copy(draft);Draft=MapRiverNodeEditing.Copy(draft);}
        bool Current=>session.RiverRevision==riverRevision&&session.HeightRevision==heightRevision&&!session.HasPending;
        public bool Update(MapRiverRecord draft,bool autoCorrect=true)
        {
            Draft=MapRiverNodeEditing.Copy(draft);
            if(!Current){Valid=false;Message="Карта изменилась во время жеста. Повторите редактирование.";return false;}
            var watch=MapRiverEditSolver.CaptureTimings?System.Diagnostics.Stopwatch.StartNew():null;
            Valid=MapRiverEditSolver.TrySolve(session,Draft,out var solved,out var error,autoCorrect,PreviewSession==null);
            LastSolveMilliseconds=watch?.Elapsed.TotalMilliseconds??0;watch?.Restart();
            if(Valid)
            {
                if(PreviewSession==null)PreviewSession=solved.preview;
                else PreviewSession.ApplyRiverEdit(solved.river);
                solved.preview=PreviewSession;
                PreviewSession.ForgetDetachedPreviewHistory();LastValid=solved;
            }
            LastPreviewApplyMilliseconds=watch?.Elapsed.TotalMilliseconds??0;
            Message=Valid?solved.message:error;return Valid;
        }
        public bool Complete()
        {
            if(!Valid||!Current){Valid=false;Message=Message??"Нет корректного предпросмотра.";return false;}
            try{session.ApplyRiverEdit(LastValid.river);Draft=session.GetRiver(Draft.id);return true;}
            catch(Exception e){Valid=false;Message=e.Message;return false;}
        }
        public MapRiverRecord Cancel()=>MapRiverNodeEditing.Copy(original);
    }
}
