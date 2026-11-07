using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    [CreateAssetMenu(menuName = "Little Castle/Experimental/Mountain Preview Settings")]
    public sealed class MountainPreviewSettings : ScriptableObject
    {
        public MountainRidgeRecipe ridge = ExampleRidge();
        public Vector2 meshMinimum = new Vector2(-125, -65);
        public Vector2 meshMaximum = new Vector2(125, 105);
        [Range(1, 8)] public float cellSize = 2;
        [Range(0, 1)] public float rockSaturation;
        public Color rockTint = new Color(.48f, .51f, .55f);
        public float snowLine = 32;
        public float snowBlend = 17;
        [Range(0, 1)] public float normalStrength;
        public bool showWaterfall = true;
        public WaterfallLinkDraft waterfall = ExampleWaterfall();

        public static MountainRidgeRecipe ExampleRidge()
        {
            var result = new MountainRidgeRecipe
            {
                id = "preview-ridge", seed = 1729, baseHeight = 0,
                roughnessAmplitude = 6, roughnessFrequency = .055f,
                pass = new MountainPass { id = "preview-pass", start = new Vector2(0,-50),
                    end = new Vector2(0,95), halfWidth = 5, floorHeight = 1, shoulderBlend = 12 }
            };
            result.nodes.Add(new RidgeNode { id="a", position=new Vector2(-105,35), height=27, halfWidth=27 });
            result.nodes.Add(new RidgeNode { id="b", position=new Vector2(-72,48), height=62, halfWidth=41 });
            result.nodes.Add(new RidgeNode { id="c", position=new Vector2(-35,32), height=34, halfWidth=32 });
            result.nodes.Add(new RidgeNode { id="d", position=new Vector2(0,47), height=49, halfWidth=36 });
            result.nodes.Add(new RidgeNode { id="e", position=new Vector2(42,54), height=82, halfWidth=44 });
            result.nodes.Add(new RidgeNode { id="f", position=new Vector2(77,39), height=39, halfWidth=33 });
            result.nodes.Add(new RidgeNode { id="g", position=new Vector2(108,48), height=57, halfWidth=28 });
            return result;
        }

        public static WaterfallLinkDraft ExampleWaterfall() => new WaterfallLinkDraft
        {
            id="preview-fall", upstreamRiverId="preview-upper", downstreamRiverId="preview-lower",
            top=new Vector3(-43,26,-8), bottom=new Vector3(-43,1,-10), width=5
        };
    }
}
