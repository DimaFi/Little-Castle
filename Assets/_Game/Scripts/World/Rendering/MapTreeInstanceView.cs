using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Selection identity; no per-tree Update, Animator or material instances.</summary>
    public sealed class MapTreeInstanceView : MonoBehaviour {public long stableId;}
}
