using NUnit.Framework;
using UnityEngine;

public class BasicTest
{
    [Test]
    public void SimpleTestPasses()
    {
        Debug.Log("Running simple test");
        Assert.Pass("Simple test passed");
    }
}