using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>Pipeline sanity check: scene load, frame stepping, and screenshot capture all work.</summary>
public class SmokeTests : HarnessFixture
{
    [UnityTest]
    public IEnumerator StartScreen_Loads_And_Captures()
    {
        yield return LoadScene("StartScreen");
        yield return null;
        Shot("smoke-startscreen");
        Assert.Pass();
    }
}
