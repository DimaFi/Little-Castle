using NUnit.Framework;
using UnityEngine;
using LittleCastle.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class LittleCastleOptionalAtmosphereTests
    {
        [Test]
        public void NewBridgeIsOptedOutByDefault()
        {
            var owner = new GameObject("optional-atmosphere-test");
            try
            {
                var bridge = owner.AddComponent<LittleCastleOptionalAtmosphereStack>();
                Assert.IsFalse(bridge.IsIntegrationRequested);
                Assert.IsFalse(bridge.IsPhysicalSkyActive);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
